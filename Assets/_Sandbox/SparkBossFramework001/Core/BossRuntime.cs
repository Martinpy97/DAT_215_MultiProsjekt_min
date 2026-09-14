using System;
using System.Collections.Generic;
using System.Linq;

namespace SparkBossFramework001.Core
{
    public sealed class BossRuntime : IBossCommands, IBossQueries
    {
        private sealed class ActiveTransition
        {
            public BossTransitionDefinition Transition { get; }
            public AttemptDirectiveCommandId CommandId { get; }
            public long TimeoutTick { get; }

            public ActiveTransition(BossTransitionDefinition transition, AttemptDirectiveCommandId commandId, long timeoutTick)
            {
                Transition = transition;
                CommandId = commandId;
                TimeoutTick = timeoutTick;
            }
        }

        private readonly BossDefinition _definition;
        private readonly IBossWorldPort _worldPort;
        private readonly BossRuntimeValidationResult _validation;
        private readonly List<BossRuntimeTrace> _trace = new();

        private bool _hasAttempt;
        private BossLifecycleState _lifecycle;
        private BossAttemptScope _attemptScope;
        private BossPhaseId _currentPhaseId;
        private long _phaseStartTick;
        private long _lastWorldTick;
        private long _decisionSeed;
        private int _staleInputCount;
        private int _staleDirectiveCount;
        private ActiveTransition _activeTransition;
        private ulong _nextDirectiveSequence;
        private string _baselineId = string.Empty;
        private EncounterDirectiveReceipt? _submittedReceipt;
        private readonly Dictionary<BossTransitionId, int> _failures = new();
        public const int TraceCapacity = 64;
        public const int MaxDirectiveAttempts = 3;

        public BossDefinition Definition => _definition;
        public IReadOnlyList<BossRuntimeTrace> Trace => _trace.AsReadOnly();
        public bool IsValid => _validation.IsValid;

        public BossRuntime(BossDefinition definition, IBossWorldPort worldPort)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _worldPort = worldPort ?? throw new ArgumentNullException(nameof(worldPort));
            _validation = ValidateDefinition(definition);
            _lifecycle = BossLifecycleState.Inactive;
            _currentPhaseId = definition.InitialPhaseId;
            _attemptScope = new BossAttemptScope(0, 0);
            _lastWorldTick = 0;
            _phaseStartTick = 0;
            _decisionSeed = 0;
        }

        public BossCommandResult StartAttempt(in BossStartCommand command)
        {
            if (!_validation.IsValid)
            {
                return new BossCommandResult(BossCommandOutcome.MissingDefinition, _validation.Message, Snapshot());
            }

            if (!command.BossId.Equals(_definition.BossId))
            {
                return new BossCommandResult(BossCommandOutcome.InvalidRequest, "Mismatched boss id", Snapshot());
            }

            if (command.Definition == null || !ReferenceEquals(command.Definition, _definition)
                || command.AttemptScope.WorldId <= 0 || command.AttemptScope.Epoch <= 0
                || command.StartTick < 0 || string.IsNullOrWhiteSpace(command.BaselineId))
            {
                return new BossCommandResult(BossCommandOutcome.InvalidRequest, "Mismatched definition", Snapshot());
            }

            if (_hasAttempt)
            {
                if (_lifecycle == BossLifecycleState.ResetReady
                    && (command.AttemptScope.WorldId != _attemptScope.WorldId || command.AttemptScope.Epoch <= _attemptScope.Epoch))
                    return new BossCommandResult(BossCommandOutcome.StaleAttempt, "New attempt requires a newer authority epoch", Snapshot());
                if (_lifecycle == BossLifecycleState.Defeated || _lifecycle == BossLifecycleState.Stopped)
                    return new BossCommandResult(BossCommandOutcome.NotReady, "Stop and prepare reset before restarting", Snapshot());
                if (_attemptScope.Equals(command.AttemptScope)
                    && (_lifecycle == BossLifecycleState.Active || _lifecycle == BossLifecycleState.Transitioning || _lifecycle == BossLifecycleState.Aborting))
                {
                    return new BossCommandResult(BossCommandOutcome.Duplicate, "Duplicate StartAttempt", Snapshot());
                }

                if (_lifecycle == BossLifecycleState.Active || _lifecycle == BossLifecycleState.Transitioning || _lifecycle == BossLifecycleState.Aborting)
                {
                    return new BossCommandResult(BossCommandOutcome.Busy, "Attempt already running", Snapshot());
                }
            }

            _hasAttempt = true;
            _lifecycle = BossLifecycleState.Active;
            _attemptScope = command.AttemptScope;
            _currentPhaseId = _definition.InitialPhaseId;
            _phaseStartTick = command.StartTick;
            _lastWorldTick = command.StartTick;
            _decisionSeed = command.DecisionSeed;
            _baselineId = command.BaselineId;
            _staleInputCount = 0;
            _staleDirectiveCount = 0;
            _activeTransition = null;
            _nextDirectiveSequence = 1;
            _trace.Clear();
            _submittedReceipt = null;
            _failures.Clear();

            return new BossCommandResult(BossCommandOutcome.Success, "Attempt started", Snapshot());
        }

        public BossCommandResult StopAttempt(in BossStopCommand command)
        {
            if (!command.BossId.Equals(_definition.BossId))
                return new BossCommandResult(BossCommandOutcome.InvalidRequest, "Mismatched boss id", Snapshot());
            if (!_hasAttempt || !_attemptScope.Equals(command.AttemptScope))
            {
                return new BossCommandResult(BossCommandOutcome.StaleAttempt, "StopAttempt stale scope", Snapshot());
            }

            if (_lifecycle == BossLifecycleState.Aborting || _lifecycle == BossLifecycleState.Inactive || _lifecycle == BossLifecycleState.Stopped)
            {
                return new BossCommandResult(BossCommandOutcome.Duplicate, "Already stopped", Snapshot());
            }

            _lifecycle = BossLifecycleState.Aborting;
            _activeTransition = null;
            _submittedReceipt = null;

            return new BossCommandResult(BossCommandOutcome.Success, "StopAttempt accepted", Snapshot());
        }

        public BossCommandResult PrepareReset(in BossPrepareResetCommand command)
        {
            if (!command.BossId.Equals(_definition.BossId))
                return new BossCommandResult(BossCommandOutcome.InvalidRequest, "Mismatched boss id", Snapshot());
            if (!_hasAttempt || !_attemptScope.Equals(command.AttemptScope))
            {
                return new BossCommandResult(BossCommandOutcome.StaleAttempt, "PrepareReset stale scope", Snapshot());
            }

            if (_lifecycle != BossLifecycleState.Aborting)
            {
                return new BossCommandResult(BossCommandOutcome.InvalidRequest, "PrepareReset requires Aborting state", Snapshot());
            }

            _lifecycle = BossLifecycleState.ResetReady;
            _activeTransition = null;
            _submittedReceipt = null;
            _trace.Clear();
            _failures.Clear();
            _decisionSeed = 0;
            _nextDirectiveSequence = 1;
            _currentPhaseId = _definition.InitialPhaseId;
            _phaseStartTick = _lastWorldTick;
            _staleInputCount = 0;
            _staleDirectiveCount = 0;
            return new BossCommandResult(BossCommandOutcome.Success, "Reset ready", Snapshot());
        }

        public BossRuntimeStepResult Step(in BossWorldView worldView)
        {
            if (!_hasAttempt || !_validation.IsValid)
            {
                return new BossRuntimeStepResult(BossStepOutcome.NotInAttempt, BossTransitionFailureKind.Unknown, "No active attempt");
            }

            if (!_attemptScope.Equals(worldView.AttemptScope))
            {
                _staleInputCount++;
                return new BossRuntimeStepResult(BossStepOutcome.StaleAttemptInput, BossTransitionFailureKind.StaleInput, "Stale attempt scope");
            }

            if (worldView.Tick < _lastWorldTick)
            {
                _staleInputCount++;
                return new BossRuntimeStepResult(BossStepOutcome.StaleAttemptInput, BossTransitionFailureKind.StaleInput, "Old logical tick");
            }

            if (_lifecycle == BossLifecycleState.Inactive || _lifecycle == BossLifecycleState.ResetReady || _lifecycle == BossLifecycleState.Stopped)
            {
                return new BossRuntimeStepResult(BossStepOutcome.NoChange, BossTransitionFailureKind.Unknown, "Lifecycle not active");
            }

            if (_lifecycle == BossLifecycleState.Defeated)
            {
                return new BossRuntimeStepResult(BossStepOutcome.Defeated, BossTransitionFailureKind.None, "Boss defeated");
            }

            if (_lifecycle == BossLifecycleState.Aborting)
            {
                return new BossRuntimeStepResult(BossStepOutcome.NoChange, BossTransitionFailureKind.Unknown, "Aborting");
            }

            _lastWorldTick = worldView.Tick;

            if (!worldView.IsBossAlive || worldView.BossHealth.IsDead)
            {
                return TransitionToDefeated(worldView);
            }

            if (_lifecycle == BossLifecycleState.Transitioning)
            {
                return ContinueTransition(in worldView);
            }

            if (!_definition.TryGetPhase(_currentPhaseId, out var phase))
            {
                _lifecycle = BossLifecycleState.Inactive;
                return new BossRuntimeStepResult(BossStepOutcome.TransitionFailed, BossTransitionFailureKind.Unknown, "Current phase missing");
            }

            var transitions = EvaluateTransitions(phase, in worldView);
            if (transitions.Count == 0)
            {
                return new BossRuntimeStepResult(BossStepOutcome.NoChange, BossTransitionFailureKind.None, "No transition condition satisfied");
            }

            var selected = transitions[0];
            return BeginTransition(in worldView, selected);
        }

        public BossReceiptSubmissionResult SubmitDirectiveReceipt(in EncounterDirectiveReceipt receipt)
        {
            if (!_hasAttempt)
            {
                return new BossReceiptSubmissionResult(BossReceiptSubmissionOutcome.RejectedStale, "No active attempt");
            }

            if (!_attemptScope.Equals(receipt.AttemptScope))
            {
                _staleDirectiveCount++;
                return new BossReceiptSubmissionResult(BossReceiptSubmissionOutcome.RejectedStale, "Receipt scope mismatch");
            }

            if (_activeTransition == null || _lifecycle != BossLifecycleState.Transitioning)
            {
                return new BossReceiptSubmissionResult(BossReceiptSubmissionOutcome.NotWaiting, "No active transition");
            }

            if (!_activeTransition.CommandId.Equals(receipt.CommandId))
            {
                return new BossReceiptSubmissionResult(BossReceiptSubmissionOutcome.UnknownCommand, "Unknown command");
            }

            // Queue until the next validated world step so death always wins this tick.
            if (!_submittedReceipt.HasValue) _submittedReceipt = receipt;
            return new BossReceiptSubmissionResult(BossReceiptSubmissionOutcome.Applied, "Receipt queued for world step");
        }

        public bool TryInspect(in BossId bossId, out BossRuntimeSnapshot snapshot)
        {
            if (bossId.Equals(_definition.BossId))
            {
                snapshot = Snapshot();
                return true;
            }

            snapshot = default;
            return false;
        }

        public BossRuntimeSnapshot Snapshot()
        {
            return new BossRuntimeSnapshot(
                _definition.BossId,
                _definition.Id,
                _attemptScope,
                _lifecycle,
                _currentPhaseId,
                _phaseStartTick,
                _lastWorldTick,
                _decisionSeed,
                _staleInputCount,
                _staleDirectiveCount,
                _trace.Count,
                _baselineId);
        }

        private BossRuntimeStepResult ContinueTransition(in BossWorldView worldView)
        {
            if (_activeTransition == null)
            {
                _lifecycle = BossLifecycleState.Active;
                return new BossRuntimeStepResult(BossStepOutcome.NoChange, BossTransitionFailureKind.Unknown, "No active transition");
            }

            var request = _activeTransition;
            if (_activeTransition.TimeoutTick > 0 && worldView.Tick >= _activeTransition.TimeoutTick)
            {
                RegisterFailure(request.Transition, request.CommandId, worldView, "Directive timeout", BossTransitionFailureKind.DirectiveTimeout);
                _activeTransition = null;
                _lifecycle = BossLifecycleState.Active;
                return new BossRuntimeStepResult(BossStepOutcome.TransitionTimeout, BossTransitionFailureKind.DirectiveTimeout, "Directive timed out");
            }

            var commandId = request.CommandId;
            EncounterDirectiveReceipt receipt;
            if (_submittedReceipt.HasValue)
            {
                receipt = _submittedReceipt.Value;
                _submittedReceipt = null;
            }
            else if (!_worldPort.TryGetDirectiveReceipt(in commandId, out receipt))
            {
                return new BossRuntimeStepResult(BossStepOutcome.TransitionWaitingDirective, BossTransitionFailureKind.None, "Waiting directive");
            }

            if (!receipt.AttemptScope.Equals(_attemptScope))
            {
                _staleDirectiveCount++;
                return new BossRuntimeStepResult(BossStepOutcome.TransitionWaitingDirective, BossTransitionFailureKind.StaleInput, "Ignored old receipt; current transition preserved");
            }

            if (!receipt.CommandId.Equals(request.CommandId))
            {
                return new BossRuntimeStepResult(BossStepOutcome.TransitionFailed, BossTransitionFailureKind.ScopeInvalid, "Command mismatch");
            }

            if (!receipt.Success)
            {
                RegisterFailure(request.Transition, request.CommandId, worldView, receipt.FailureMessage);
                _activeTransition = null;
                _lifecycle = BossLifecycleState.Active;
                return new BossRuntimeStepResult(BossStepOutcome.TransitionFailed, BossTransitionFailureKind.DirectiveFailed, "Directive failed");
            }

            CommitTransition(request.Transition, worldView, "Directive accepted");
            _activeTransition = null;
            _lifecycle = BossLifecycleState.Active;
            return new BossRuntimeStepResult(BossStepOutcome.TransitionCommitted, BossTransitionFailureKind.None, "Transition committed");
        }

        private BossRuntimeStepResult BeginTransition(in BossWorldView worldView, in Candidate candidate)
        {
            var transition = candidate.Transition;
            if (!transition.RequiredDirective.HasValue)
            {
                CommitTransition(transition, worldView, candidate.Trace);
                return new BossRuntimeStepResult(BossStepOutcome.TransitionCommitted, BossTransitionFailureKind.None, "Transition committed");
            }

            var commandId = nextCommandId();
            _submittedReceipt = null;
            var requirement = transition.RequiredDirective.Value;
            var request = new EncounterDirectiveRequest(
                _definition.BossId,
                _attemptScope,
                transition.Id,
                transition.SourcePhaseId,
                transition.TargetPhaseId,
                commandId,
                requirement.DirectiveType,
                candidate.Trace,
                requirement.TimeoutTicks,
                worldView.Tick);

            var dispatch = _worldPort.DispatchEncounterDirective(in request);
            if (!dispatch.Dispatched || !dispatch.CommandId.Equals(commandId))
            {
                RegisterFailure(transition, commandId, worldView, dispatch.FailureMessage, ToFailureKind(dispatch.FailureKind));
                _lifecycle = BossLifecycleState.Active;
                return new BossRuntimeStepResult(BossStepOutcome.TransitionFailed, BossTransitionFailureKind.DirectiveRejected, "Directive rejected");
            }

            if (dispatch.CompletedImmediately)
            {
                if (dispatch.Success)
                {
                    CommitTransition(transition, worldView, "Immediate directive success");
                    return new BossRuntimeStepResult(BossStepOutcome.TransitionCommitted, BossTransitionFailureKind.None, "Immediate directive success");
                }

                RegisterFailure(transition, commandId, worldView, dispatch.FailureMessage);
                return new BossRuntimeStepResult(BossStepOutcome.TransitionFailed, BossTransitionFailureKind.DirectiveFailed, "Immediate directive failed");
            }

            _activeTransition = new ActiveTransition(transition, commandId, requirement.TimeoutTicks > 0
                ? worldView.Tick + requirement.TimeoutTicks
                : 0);
            _lifecycle = BossLifecycleState.Transitioning;
            AddTrace(new BossRuntimeTrace(
                transition.Id,
                _currentPhaseId,
                transition.TargetPhaseId,
                candidate.Trace,
                BossStepOutcome.TransitionWaitingDirective,
                BossTransitionFailureKind.None,
                commandId,
                worldView.Tick,
                "Awaiting required directive"));

            return new BossRuntimeStepResult(BossStepOutcome.TransitionWaitingDirective, BossTransitionFailureKind.None, "Awaiting required directive");
        }

        private void RegisterFailure(
            BossTransitionDefinition transition,
            AttemptDirectiveCommandId commandId,
            in BossWorldView worldView,
            string message,
            BossTransitionFailureKind failureKind = BossTransitionFailureKind.DirectiveFailed)
        {
            _failures.TryGetValue(transition.Id, out var failures);
            _failures[transition.Id] = failures + 1;
            _submittedReceipt = null;
            AddTrace(new BossRuntimeTrace(
                transition.Id,
                _currentPhaseId,
                _currentPhaseId,
                message,
                BossStepOutcome.TransitionFailed,
                failureKind,
                commandId,
                worldView.Tick,
                "Transition failed"));
        }

        private void CommitTransition(BossTransitionDefinition transition, in BossWorldView worldView, string note)
        {
            var source = _currentPhaseId;
            _currentPhaseId = transition.TargetPhaseId;
            _phaseStartTick = worldView.Tick;
            AddTrace(new BossRuntimeTrace(
                transition.Id,
                source,
                transition.TargetPhaseId,
                note,
                BossStepOutcome.TransitionCommitted,
                BossTransitionFailureKind.None,
                null,
                worldView.Tick,
                "Phase committed"));
        }

        private BossRuntimeStepResult TransitionToDefeated(in BossWorldView worldView)
        {
            _lifecycle = BossLifecycleState.Defeated;
            _activeTransition = null;
            AddTrace(new BossRuntimeTrace(
                new BossTransitionId("DEFEAT"),
                _currentPhaseId,
                _currentPhaseId,
                "BossHealth<=0 or IsBossAlive=false",
                BossStepOutcome.Defeated,
                BossTransitionFailureKind.None,
                null,
                worldView.Tick,
                "Death precedence"));

            return new BossRuntimeStepResult(BossStepOutcome.Defeated, BossTransitionFailureKind.None, "Boss defeated");
        }

        private static BossTransitionFailureKind ToFailureKind(EncounterDirectiveFailureKind failureKind)
        {
            return failureKind switch
            {
                EncounterDirectiveFailureKind.Busy => BossTransitionFailureKind.DirectiveRejected,
                EncounterDirectiveFailureKind.Timeout => BossTransitionFailureKind.DirectiveTimeout,
                EncounterDirectiveFailureKind.Rejected => BossTransitionFailureKind.DirectiveRejected,
                EncounterDirectiveFailureKind.None => BossTransitionFailureKind.None,
                _ => BossTransitionFailureKind.Unknown
            };
        }

        private List<Candidate> EvaluateTransitions(BossPhaseDefinition phase, in BossWorldView worldView)
        {
            var context = new BossTransitionContext(worldView, _currentPhaseId, _phaseStartTick);
            var candidates = new List<Candidate>(phase.GetTransitions().Count);
            var transitions = phase.GetTransitions();

            for (var i = 0; i < transitions.Count; i++)
            {
                var transition = transitions[i];
                if (_failures.TryGetValue(transition.Id, out var failures) && failures >= MaxDirectiveAttempts) continue;
                var eval = transition.Condition?.Evaluate(in context);
                if (eval.HasValue && eval.Value.IsSatisfied)
                {
                    candidates.Add(new Candidate(transition, eval.Value.Trace));
                }
            }

            if (candidates.Count <= 1)
            {
                return candidates;
            }

            candidates.Sort((left, right) =>
            {
                if (left.Transition.Priority != right.Transition.Priority)
                {
                    return right.Transition.Priority.CompareTo(left.Transition.Priority);
                }

                if (left.Transition.SourceDeclarationOrder != right.Transition.SourceDeclarationOrder)
                {
                    return left.Transition.SourceDeclarationOrder.CompareTo(right.Transition.SourceDeclarationOrder);
                }

                return left.Transition.DeclarationOrder.CompareTo(right.Transition.DeclarationOrder);
            });

            return candidates;
        }

        private AttemptDirectiveCommandId nextCommandId()
        {
            return new AttemptDirectiveCommandId(_nextDirectiveSequence++);
        }

        private BossRuntimeValidationResult ValidateDefinition(BossDefinition definition)
        {
            return BossDefinitionValidation.Validate(definition);
        }

        private void AddTrace(BossRuntimeTrace trace)
        {
            if (_trace.Count == TraceCapacity) _trace.RemoveAt(0);
            _trace.Add(trace);
        }

        private readonly struct Candidate
        {
            public BossTransitionDefinition Transition { get; }
            public string Trace { get; }

            public Candidate(BossTransitionDefinition transition, string trace)
            {
                Transition = transition;
                Trace = trace;
            }
        }
    }
}
