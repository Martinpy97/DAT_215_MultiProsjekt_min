using System.Collections.Generic;

namespace SparkBossFramework001.Core
{
    public readonly struct BossStartCommand
    {
        public BossId BossId { get; }
        public BossDefinition Definition { get; }
        public BossAttemptScope AttemptScope { get; }
        public long StartTick { get; }
        public int DecisionSeed { get; }
        public string BaselineId { get; }

        public BossStartCommand(
            BossId bossId,
            BossDefinition definition,
            BossAttemptScope attemptScope,
            long startTick,
            int decisionSeed,
            string baselineId)
        {
            BossId = bossId;
            Definition = definition;
            AttemptScope = attemptScope;
            StartTick = startTick;
            DecisionSeed = decisionSeed;
            BaselineId = baselineId;
        }
    }

    public readonly struct BossStopCommand
    {
        public BossId BossId { get; }
        public BossAttemptScope AttemptScope { get; }
        public string Reason { get; }

        public BossStopCommand(BossId bossId, BossAttemptScope attemptScope, string reason)
        {
            BossId = bossId;
            AttemptScope = attemptScope;
            Reason = reason;
        }
    }

    public readonly struct BossPrepareResetCommand
    {
        public BossId BossId { get; }
        public BossAttemptScope AttemptScope { get; }

        public BossPrepareResetCommand(BossId bossId, BossAttemptScope attemptScope)
        {
            BossId = bossId;
            AttemptScope = attemptScope;
        }
    }

    public sealed class BossCommandResult
    {
        public BossCommandOutcome Outcome { get; }
        public string Message { get; }
        public BossRuntimeSnapshot Snapshot { get; }

        public BossCommandResult(BossCommandOutcome outcome, string message, BossRuntimeSnapshot snapshot)
        {
            Outcome = outcome;
            Message = message;
            Snapshot = snapshot;
        }

        public bool IsSuccess => Outcome == BossCommandOutcome.Success;
        public bool IsDuplicate => Outcome == BossCommandOutcome.Duplicate;
    }

    public readonly struct BossWorldFact
    {
        public string FactId { get; }
        public bool Value { get; }
        public long Revision { get; }

        public BossWorldFact(string factId, bool value, long revision)
        {
            FactId = factId;
            Value = value;
            Revision = revision;
        }
    }

    public readonly struct BossWorldView
    {
        public BossAttemptScope AttemptScope { get; }
        public HealthFraction BossHealth { get; }
        public bool IsBossAlive { get; }
        public long Tick { get; }
        public long HealthRevision { get; }
        public long ArenaRevision { get; }
        public IReadOnlyDictionary<string, BossWorldFact> ArenaFacts { get; }

        public BossWorldView(
            BossAttemptScope attemptScope,
            HealthFraction bossHealth,
            bool isBossAlive,
            long tick,
            long healthRevision,
            long arenaRevision,
            IReadOnlyDictionary<string, BossWorldFact> arenaFacts)
        {
            AttemptScope = attemptScope;
            BossHealth = bossHealth;
            IsBossAlive = isBossAlive;
            Tick = tick;
            HealthRevision = healthRevision;
            ArenaRevision = arenaRevision;
            var copy = new Dictionary<string, BossWorldFact>();
            if (arenaFacts != null) foreach (var pair in arenaFacts) copy.Add(pair.Key, pair.Value);
            ArenaFacts = new System.Collections.ObjectModel.ReadOnlyDictionary<string, BossWorldFact>(copy);
        }
    }

    public readonly struct EncounterDirectiveRequirement
    {
        public string DirectiveType { get; }
        public long TimeoutTicks { get; }

        public EncounterDirectiveRequirement(string directiveType, long timeoutTicks)
        {
            DirectiveType = directiveType;
            TimeoutTicks = timeoutTicks;
        }
    }

    public readonly struct EncounterDirectiveRequest
    {
        public BossId BossId { get; }
        public BossAttemptScope AttemptScope { get; }
        public BossTransitionId TransitionId { get; }
        public BossPhaseId SourcePhaseId { get; }
        public BossPhaseId TargetPhaseId { get; }
        public AttemptDirectiveCommandId CommandId { get; }
        public string DirectiveType { get; }
        public string DebugReason { get; }
        public long TimeoutTicks { get; }
        public long AttemptedTick { get; }

        public EncounterDirectiveRequest(
            BossId bossId,
            BossAttemptScope attemptScope,
            BossTransitionId transitionId,
            BossPhaseId sourcePhaseId,
            BossPhaseId targetPhaseId,
            AttemptDirectiveCommandId commandId,
            string directiveType,
            string debugReason,
            long timeoutTicks,
            long attemptedTick)
        {
            BossId = bossId;
            AttemptScope = attemptScope;
            TransitionId = transitionId;
            SourcePhaseId = sourcePhaseId;
            TargetPhaseId = targetPhaseId;
            CommandId = commandId;
            DirectiveType = directiveType;
            DebugReason = debugReason;
            TimeoutTicks = timeoutTicks;
            AttemptedTick = attemptedTick;
        }
    }

    public readonly struct EncounterDirectiveReceipt
    {
        public AttemptDirectiveCommandId CommandId { get; }
        public BossAttemptScope AttemptScope { get; }
        public bool Success { get; }
        public string FailureMessage { get; }
        public EncounterDirectiveFailureKind FailureKind { get; }

        public EncounterDirectiveReceipt(
            AttemptDirectiveCommandId commandId,
            BossAttemptScope attemptScope,
            bool success,
            EncounterDirectiveFailureKind failureKind,
            string failureMessage)
        {
            CommandId = commandId;
            AttemptScope = attemptScope;
            Success = success;
            FailureKind = failureKind;
            FailureMessage = failureMessage;
        }
    }

    public readonly struct EncounterDirectiveDispatchResult
    {
        public bool Dispatched { get; }
        public AttemptDirectiveCommandId CommandId { get; }
        public bool CompletedImmediately { get; }
        public bool Success { get; }
        public EncounterDirectiveFailureKind FailureKind { get; }
        public string FailureMessage { get; }

        public EncounterDirectiveDispatchResult(
            bool dispatched,
            AttemptDirectiveCommandId commandId,
            bool completedImmediately,
            bool success,
            EncounterDirectiveFailureKind failureKind,
            string failureMessage)
        {
            Dispatched = dispatched;
            CommandId = commandId;
            CompletedImmediately = completedImmediately;
            Success = success;
            FailureKind = failureKind;
            FailureMessage = failureMessage;
        }

        public static EncounterDirectiveDispatchResult DispatchFailed(
            AttemptDirectiveCommandId commandId,
            EncounterDirectiveFailureKind failureKind,
            string message)
        {
            return new EncounterDirectiveDispatchResult(
                false,
                commandId,
                false,
                false,
                failureKind,
                message);
        }

        public static EncounterDirectiveDispatchResult DispatchSuccess(AttemptDirectiveCommandId commandId)
        {
            return new EncounterDirectiveDispatchResult(
                true,
                commandId,
                true,
                true,
                EncounterDirectiveFailureKind.None,
                "Immediate success");
        }

        public static EncounterDirectiveDispatchResult DispatchPending(AttemptDirectiveCommandId commandId)
        {
            return new EncounterDirectiveDispatchResult(
                true,
                commandId,
                false,
                false,
                EncounterDirectiveFailureKind.None,
                "Pending");
        }
    }

    public readonly struct BossRuntimeStepResult
    {
        public BossStepOutcome Outcome { get; }
        public BossTransitionFailureKind FailureKind { get; }
        public string Message { get; }

        public BossRuntimeStepResult(BossStepOutcome outcome, BossTransitionFailureKind failureKind, string message)
        {
            Outcome = outcome;
            FailureKind = failureKind;
            Message = message;
        }
    }

    public enum BossReceiptSubmissionOutcome
    {
        Unknown,
        Applied,
        RejectedStale,
        UnknownCommand,
        NotWaiting
    }

    public readonly struct BossReceiptSubmissionResult
    {
        public BossReceiptSubmissionOutcome Outcome { get; }
        public string Message { get; }

        public BossReceiptSubmissionResult(BossReceiptSubmissionOutcome outcome, string message)
        {
            Outcome = outcome;
            Message = message;
        }
    }

    public interface IBossCommands
    {
        BossCommandResult StartAttempt(in BossStartCommand command);
        BossCommandResult StopAttempt(in BossStopCommand command);
        BossCommandResult PrepareReset(in BossPrepareResetCommand command);
    }

    public interface IBossQueries
    {
        bool TryInspect(in BossId bossId, out BossRuntimeSnapshot snapshot);
    }

    public interface IBossWorldPort
    {
        EncounterDirectiveDispatchResult DispatchEncounterDirective(in EncounterDirectiveRequest request);
        bool TryGetDirectiveReceipt(in AttemptDirectiveCommandId commandId, out EncounterDirectiveReceipt receipt);
    }
}
