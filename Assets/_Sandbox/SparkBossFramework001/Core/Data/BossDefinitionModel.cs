using System.Collections.Generic;
using System.Linq;

namespace SparkBossFramework001.Core
{
    public sealed class BossDefinition
    {
        public BossDefinitionId Id { get; }
        public BossId BossId { get; }
        public string Label { get; }
        public BossPhaseId InitialPhaseId { get; }
        public IReadOnlyList<BossPhaseDefinition> Phases => System.Array.AsReadOnly(_phases);

        private readonly BossPhaseDefinition[] _phases;
        private readonly Dictionary<string, BossPhaseDefinition> _phaseIndex = new();

        public BossDefinition(
            BossDefinitionId id,
            BossId bossId,
            string label,
            BossPhaseId initialPhaseId,
            IEnumerable<BossPhaseDefinition> phases)
        {
            Id = id;
            BossId = bossId;
            Label = label;
            InitialPhaseId = initialPhaseId;
            _phases = phases?.ToArray() ?? new BossPhaseDefinition[0];

            for (var i = 0; i < _phases.Length; i++)
            {
                var phase = _phases[i];
                if (phase == null || string.IsNullOrWhiteSpace(phase.Id.Value)) continue;
                phase.InitializeIndexes(i);
                _phaseIndex[phase.Id.Value] = phase;
            }
        }

        public bool TryGetPhase(BossPhaseId phaseId, out BossPhaseDefinition phase)
        {
            phase = null;
            return phaseId.Value != null && _phaseIndex.TryGetValue(phaseId.Value, out phase);
        }

        public BossPhaseDefinition InitialPhase()
        {
            TryGetPhase(InitialPhaseId, out var phase);
            return phase;
        }
    }

    public sealed class BossPhaseDefinition
    {
        public BossPhaseId Id { get; }
        public string Label { get; }
        public IReadOnlyList<BossTransitionDefinition> Transitions => System.Array.AsReadOnly(_transitions);

        internal int DeclarationOrder { get; private set; }
        private readonly BossTransitionDefinition[] _transitions;

        public BossPhaseDefinition(BossPhaseId id, string label, IEnumerable<BossTransitionDefinition> transitions)
        {
            Id = id;
            Label = label;
            _transitions = transitions?.ToArray() ?? new BossTransitionDefinition[0];
        }

        public IReadOnlyList<BossTransitionDefinition> GetTransitions()
        {
            return Transitions;
        }

        internal void InitializeIndexes(int declarationOrder)
        {
            DeclarationOrder = declarationOrder;

            for (var i = 0; i < _transitions.Length; i++)
            {
                _transitions[i]?.InitializeIndexes(Id, declarationOrder, i);
            }
        }
    }

    public sealed class BossTransitionDefinition
    {
        public BossTransitionId Id { get; }
        public BossPhaseId SourcePhaseId { get; private set; }
        public BossPhaseId TargetPhaseId { get; }
        public int Priority { get; }
        public ITransitionCondition Condition { get; }
        public EncounterDirectiveRequirement? RequiredDirective { get; }
        public int SourceDeclarationOrder { get; private set; }
        public int DeclarationOrder { get; private set; }

        public BossTransitionDefinition(
            BossTransitionId id,
            BossPhaseId sourcePhaseId,
            BossPhaseId targetPhaseId,
            int priority,
            ITransitionCondition condition,
            EncounterDirectiveRequirement? requiredDirective = null)
        {
            Id = id;
            SourcePhaseId = sourcePhaseId;
            TargetPhaseId = targetPhaseId;
            Priority = priority;
            Condition = condition;
            RequiredDirective = requiredDirective;
        }

        internal void InitializeIndexes(BossPhaseId sourcePhaseId, int sourceDeclarationOrder, int declarationOrder)
        {
            SourcePhaseId = sourcePhaseId;
            SourceDeclarationOrder = sourceDeclarationOrder;
            DeclarationOrder = declarationOrder;
        }
    }

    public sealed class BossRuntimeTrace
    {
        public BossTransitionId TransitionId { get; }
        public BossPhaseId From { get; }
        public BossPhaseId To { get; }
        public string ConditionTrace { get; }
        public BossStepOutcome Outcome { get; }
        public BossTransitionFailureKind FailureKind { get; }
        public AttemptDirectiveCommandId? RequiredDirectiveCommandId { get; }
        public long Tick { get; }
        public string Notes { get; }

        public BossRuntimeTrace(
            BossTransitionId transitionId,
            BossPhaseId from,
            BossPhaseId to,
            string conditionTrace,
            BossStepOutcome outcome,
            BossTransitionFailureKind failureKind,
            AttemptDirectiveCommandId? requiredDirectiveCommandId,
            long tick,
            string notes)
        {
            TransitionId = transitionId;
            From = from;
            To = to;
            ConditionTrace = conditionTrace;
            Outcome = outcome;
            FailureKind = failureKind;
            RequiredDirectiveCommandId = requiredDirectiveCommandId;
            Tick = tick;
            Notes = notes;
        }
    }

    public readonly struct BossRuntimeSnapshot
    {
        public BossId BossId { get; }
        public BossDefinitionId DefinitionId { get; }
        public BossAttemptScope AttemptScope { get; }
        public BossLifecycleState Lifecycle { get; }
        public BossPhaseId CurrentPhaseId { get; }
        public long PhaseStartTick { get; }
        public long LastWorldTick { get; }
        public long DecisionSeed { get; }
        public int StaleInputCount { get; }
        public int StaleDirectiveCount { get; }
        public int TraceDepth { get; }
        public string BaselineId { get; }

        public BossRuntimeSnapshot(
            BossId bossId,
            BossDefinitionId definitionId,
            BossAttemptScope attemptScope,
            BossLifecycleState lifecycle,
            BossPhaseId currentPhaseId,
            long phaseStartTick,
            long lastWorldTick,
            long decisionSeed,
            int staleInputCount,
            int staleDirectiveCount,
            int traceDepth,
            string baselineId)
        {
            BossId = bossId;
            DefinitionId = definitionId;
            AttemptScope = attemptScope;
            Lifecycle = lifecycle;
            CurrentPhaseId = currentPhaseId;
            PhaseStartTick = phaseStartTick;
            LastWorldTick = lastWorldTick;
            DecisionSeed = decisionSeed;
            StaleInputCount = staleInputCount;
            StaleDirectiveCount = staleDirectiveCount;
            TraceDepth = traceDepth;
            BaselineId = baselineId;
        }
    }

    public sealed class BossRuntimeValidationResult
    {
        public bool IsValid { get; }
        public string Message { get; }

        public BossRuntimeValidationResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message;
        }
    }
}
