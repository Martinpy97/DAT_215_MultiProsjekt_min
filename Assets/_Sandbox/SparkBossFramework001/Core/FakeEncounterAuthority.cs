using System;
using System.Collections.Generic;
using SparkBossFramework001.Core;

namespace SparkBossFramework001.Sandbox
{
    // Sandbox world owner: this is not a production health/encounter adapter.
    public sealed class FakeEncounterAuthority : IBossWorldPort
    {
        public static readonly BossPhaseId P1 = new BossPhaseId("P1_Awakening");
        public static readonly BossPhaseId P2 = new BossPhaseId("P2_Sealed");
        public static readonly BossPhaseId P3 = new BossPhaseId("P3_Unbound");
        private readonly Dictionary<AttemptDirectiveCommandId, EncounterDirectiveReceipt> _receipts = new();
        private long _epoch;
        private long _revision;
        public BossRuntime Runtime { get; }
        public BossAttemptScope Scope { get; private set; }
        public long Tick { get; private set; }
        public int HealthPercent { get; private set; } = 100;
        public bool RunesComplete { get; private set; }
        public bool IsAlive { get; private set; } = true;
        public bool AdmissionsOpen { get; private set; }
        public bool AutoAcknowledge { get; set; } = true;
        public int DispatchCount { get; private set; }
        public EncounterDirectiveRequest LastRequest { get; private set; }
        public BossRuntimeSnapshot LastResetReady { get; private set; }

        public FakeEncounterAuthority(BossDefinition definition = null)
        {
            Runtime = new BossRuntime(definition ?? FixtureDefinition(), this);
        }

        public BossCommandResult Start(int seed = 123)
        {
            if (_epoch != 0) throw new InvalidOperationException("Use Reset after the initial attempt");
            return RestoreAndStart(seed);
        }

        public BossCommandResult Reset(int seed = 123)
        {
            AdmissionsOpen = false; // Invalidate A before either cleanup command.
            var boss = Runtime.Definition.BossId;
            Require(Runtime.StopAttempt(new BossStopCommand(boss, Scope, "SandboxReset")));
            Require(Runtime.PrepareReset(new BossPrepareResetCommand(boss, Scope)));
            LastResetReady = Runtime.Snapshot();
            return RestoreAndStart(seed);
        }

        private BossCommandResult RestoreAndStart(int seed)
        {
            HealthPercent = 100;
            IsAlive = true;
            RunesComplete = false;
            Tick = 0;
            _revision = 1;
            _receipts.Clear();
            DispatchCount = 0;
            Scope = new BossAttemptScope(215, ++_epoch);
            var result = Runtime.StartAttempt(new BossStartCommand(Runtime.Definition.BossId, Runtime.Definition, Scope, Tick, seed, "SandboxBaseline-v1"));
            AdmissionsOpen = result.IsSuccess;
            Require(result);
            return result;
        }

        public void SetHealth(int percent, bool alive = true) { HealthPercent = percent; IsAlive = alive; _revision++; }
        public void SetRunes(bool value) { RunesComplete = value; _revision++; }
        public BossWorldView Capture(long? tick = null)
        {
            return new BossWorldView(Scope, new HealthFraction(HealthPercent), IsAlive, tick ?? Tick, _revision, _revision,
                new Dictionary<string, BossWorldFact> { { "GuardianRunesComplete", new BossWorldFact("GuardianRunesComplete", RunesComplete, _revision) } });
        }
        public BossRuntimeStepResult Step(long elapsedTicks = 1)
        {
            if (!AdmissionsOpen) throw new InvalidOperationException("Attempt admissions frozen");
            Tick += elapsedTicks;
            return Runtime.Step(Capture());
        }
        public EncounterDirectiveDispatchResult DispatchEncounterDirective(in EncounterDirectiveRequest request)
        {
            if (!AdmissionsOpen || !request.AttemptScope.Equals(Scope))
                return EncounterDirectiveDispatchResult.DispatchFailed(request.CommandId, EncounterDirectiveFailureKind.Rejected, "Invalid attempt");
            LastRequest = request;
            if (_receipts.TryGetValue(request.CommandId, out var existing))
                return new EncounterDirectiveDispatchResult(true, request.CommandId, true, existing.Success, existing.FailureKind, existing.FailureMessage);
            DispatchCount++;
            if (!AutoAcknowledge) return EncounterDirectiveDispatchResult.DispatchPending(request.CommandId);
            _receipts.Add(request.CommandId, new EncounterDirectiveReceipt(request.CommandId, Scope, true, EncounterDirectiveFailureKind.None, "Fake world applied"));
            return EncounterDirectiveDispatchResult.DispatchSuccess(request.CommandId);
        }
        public bool TryGetDirectiveReceipt(in AttemptDirectiveCommandId commandId, out EncounterDirectiveReceipt receipt) => _receipts.TryGetValue(commandId, out receipt);
        public void Receipt(bool success) => _receipts[LastRequest.CommandId] = new EncounterDirectiveReceipt(LastRequest.CommandId, Scope, success,
            success ? EncounterDirectiveFailureKind.None : EncounterDirectiveFailureKind.Rejected, success ? "Fake success" : "Fake failure");
        private static void Require(BossCommandResult result) { if (!result.IsSuccess && !result.IsDuplicate) throw new InvalidOperationException(result.Message); }

        public static BossDefinition FixtureDefinition()
        {
            return new BossDefinition(new BossDefinitionId("MummyGuardian-Sandbox-v1"), new BossId("MummyGuardian_Sandbox"), "BOSS-001", P1,
                new[] {
                    new BossPhaseDefinition(P1, "Awakening", new[] { new BossTransitionDefinition(new BossTransitionId("HP70"), P1, P2, 10, new HealthAtOrBelowCondition(70)) }),
                    new BossPhaseDefinition(P2, "Sealed", new[] { new BossTransitionDefinition(new BossTransitionId("Runes"), P2, P3, 10,
                        new ArenaFactEqualsCondition("GuardianRunesComplete", true), new EncounterDirectiveRequirement("DisableBossShield", 5)) }),
                    new BossPhaseDefinition(P3, "Unbound", Array.Empty<BossTransitionDefinition>())
                });
        }
    }
}
