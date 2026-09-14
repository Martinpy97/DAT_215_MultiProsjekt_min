using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace SparkBossFramework001.Tests.EditMode
{
    using SparkBossFramework001.Core;

    [TestFixture]
    public sealed class BossRuntimeTests
    {
        [Test]
        public void T1_HP_Transition_From_P1_To_P2_Occurs_Once()
        {
            var runtime = BuildRuntimeWithDefinition(BossFixtureDefinition_HealthPhaseFlow(), out var authority);
            var scope = new BossAttemptScope(1, 1);

            var start = runtime.StartAttempt(new BossStartCommand(
                new BossId("MummyGuardian_Sandbox"),
                runtime.Definition,
                scope,
                0,
                123,
                "baseline-A"));
            Assert.AreEqual(BossCommandOutcome.Success, start.Outcome);

            var unchanged = runtime.Step(World(scope, tick: 1, hpPercent: 100, isAlive: true));
            Assert.AreEqual(BossStepOutcome.NoChange, unchanged.Outcome);

            var transition = runtime.Step(World(scope, tick: 2, hpPercent: 69, isAlive: true));
            Assert.AreEqual(BossStepOutcome.TransitionCommitted, transition.Outcome);
            Assert.AreEqual(new BossPhaseId("P2_Sealed"), runtime.Snapshot().CurrentPhaseId);

            var notRepeated = runtime.Step(World(scope, tick: 3, hpPercent: 60, isAlive: true));
            Assert.AreEqual(BossStepOutcome.NoChange, notRepeated.Outcome);
            Assert.AreEqual(1, runtime.Trace.Count(t => t.TransitionId.Value == "T1_A"));
        }

        [Test]
        public void T2_Arena_Fact_Transition_From_P2_To_P3()
        {
            var runtime = BuildRuntimeWithDefinition(BossFixtureDefinition_ArenaPhaseFlow(), out _);
            var scope = new BossAttemptScope(1, 1);

            runtime.StartAttempt(new BossStartCommand(
                new BossId("MummyGuardian_Sandbox"),
                runtime.Definition,
                scope,
                0,
                11,
                "baseline"));

            var noChange = runtime.Step(World(scope, tick: 2, hpPercent: 100, isAlive: true, arenaFacts: null));
            Assert.AreEqual(BossStepOutcome.NoChange, noChange.Outcome);
            Assert.AreEqual(new BossPhaseId("P2_Sealed"), runtime.Snapshot().CurrentPhaseId);

            var toP3 = runtime.Step(World(scope, tick: 3, hpPercent: 100, isAlive: true, arenaFacts: new Dictionary<string, BossWorldFact>
            {
                {"GuardianRunesComplete", new BossWorldFact("GuardianRunesComplete", true, 2)}
            }));

            Assert.AreEqual(BossStepOutcome.TransitionCommitted, toP3.Outcome);
            Assert.AreEqual(new BossPhaseId("P3_Unbound"), runtime.Snapshot().CurrentPhaseId);
        }

        [Test]
        public void T3_Simultaneous_Transitions_Use_Deterministic_Priority_And_Definition_Order()
        {
            var runtime = BuildRuntimeWithDefinition(BossFixtureDefinition_Simultaneous(), out _);
            var scope = new BossAttemptScope(1, 2);

            runtime.StartAttempt(new BossStartCommand(new BossId("MummyGuardian_Sandbox"), runtime.Definition, scope, 0, 11, "baseline"));
            var result = runtime.Step(World(scope, tick: 1, hpPercent: 60, isAlive: true, arenaFacts: new Dictionary<string, BossWorldFact>
            {
                { "GuardianRunesComplete", new BossWorldFact("GuardianRunesComplete", false, 1) }
            }));

            Assert.AreEqual(BossStepOutcome.TransitionCommitted, result.Outcome);
            Assert.AreEqual(new BossPhaseId("P3_Elite"), runtime.Snapshot().CurrentPhaseId);
            Assert.AreEqual("T2_B", runtime.Trace.Last().TransitionId.Value);
        }

        [Test]
        public void T4_Required_Directive_Waits_For_Receipt_And_Records_Failure()
        {
            var runtime = BuildRuntimeWithDefinition(BossFixtureDefinition_WithDirective(), out var authority);
            var scope = new BossAttemptScope(7, 1);

            runtime.StartAttempt(new BossStartCommand(new BossId("MummyGuardian_Sandbox"), runtime.Definition, scope, 0, 4, "baseline"));
            var waiting = runtime.Step(World(scope, tick: 1, hpPercent: 70, isAlive: true));
            Assert.AreEqual(BossStepOutcome.TransitionWaitingDirective, waiting.Outcome);
            Assert.AreEqual(new BossPhaseId("P1_Awakening"), runtime.Snapshot().CurrentPhaseId);

            var stillWaiting = runtime.Step(World(scope, tick: 2, hpPercent: 70, isAlive: true));
            Assert.AreEqual(BossStepOutcome.TransitionWaitingDirective, stillWaiting.Outcome);

            var commandId = authority.LastDispatchCommandId;
            authority.InjectReceipt(new EncounterDirectiveReceipt(commandId, scope, false, EncounterDirectiveFailureKind.Rejected, "directive failed"));

            var failed = runtime.Step(World(scope, tick: 3, hpPercent: 70, isAlive: true));
            Assert.AreEqual(BossStepOutcome.TransitionFailed, failed.Outcome);
            Assert.AreEqual(new BossPhaseId("P1_Awakening"), runtime.Snapshot().CurrentPhaseId);
        }

        [Test]
        public void T5_Death_Has_Precedence_Over_Transition()
        {
            var runtime = BuildRuntimeWithDefinition(BossFixtureDefinition_HealthPhaseFlow(), out _);
            var scope = new BossAttemptScope(1, 3);

            runtime.StartAttempt(new BossStartCommand(new BossId("MummyGuardian_Sandbox"), runtime.Definition, scope, 0, 11, "baseline"));
            var died = runtime.Step(World(scope, tick: 1, hpPercent: 40, isAlive: false));

            Assert.AreEqual(BossStepOutcome.Defeated, died.Outcome);
            Assert.AreEqual(BossLifecycleState.Defeated, runtime.Snapshot().Lifecycle);
            Assert.AreEqual(new BossPhaseId("P1_Awakening"), runtime.Snapshot().CurrentPhaseId);
        }

        [Test]
        public void T6_Reset_Prepares_New_Attempt_From_Baseline()
        {
            var runtime = BuildRuntimeWithDefinition(BossFixtureDefinition_HealthPhaseFlow(), out _);
            var scopeA = new BossAttemptScope(1, 10);

            runtime.StartAttempt(new BossStartCommand(new BossId("MummyGuardian_Sandbox"), runtime.Definition, scopeA, 0, 11, "baseline-old"));
            var toP2 = runtime.Step(World(scopeA, tick: 1, hpPercent: 69, isAlive: true));
            Assert.AreEqual(BossStepOutcome.TransitionCommitted, toP2.Outcome);
            Assert.AreEqual(new BossPhaseId("P2_Sealed"), runtime.Snapshot().CurrentPhaseId);

            runtime.StopAttempt(new BossStopCommand(new BossId("MummyGuardian_Sandbox"), scopeA, "PlayerDied"));
            runtime.PrepareReset(new BossPrepareResetCommand(new BossId("MummyGuardian_Sandbox"), scopeA));

            var scopeB = new BossAttemptScope(1, 11);
            var restarted = runtime.StartAttempt(new BossStartCommand(new BossId("MummyGuardian_Sandbox"), runtime.Definition, scopeB, 10, 11, "baseline-new"));
            Assert.AreEqual(BossCommandOutcome.Success, restarted.Outcome);
            Assert.AreEqual(new BossPhaseId("P1_Awakening"), runtime.Snapshot().CurrentPhaseId);

            var toP2Again = runtime.Step(World(scopeB, tick: 12, hpPercent: 69, isAlive: true));
            Assert.AreEqual(BossStepOutcome.TransitionCommitted, toP2Again.Outcome);
            Assert.AreEqual(new BossPhaseId("P2_Sealed"), runtime.Snapshot().CurrentPhaseId);
            Assert.AreEqual("baseline-new", runtime.Snapshot().BaselineId);
        }

        [Test]
        public void T7_Stale_Receipts_From_Previous_Attempt_Do_Not_Mutate_Current_Attempt()
        {
            var runtime = BuildRuntimeWithDefinition(BossFixtureDefinition_WithDirective(), out var authority);
            var scopeA = new BossAttemptScope(2, 1);
            runtime.StartAttempt(new BossStartCommand(new BossId("MummyGuardian_Sandbox"), runtime.Definition, scopeA, 0, 42, "old"));
            var waitingA = runtime.Step(World(scopeA, tick: 1, hpPercent: 60, isAlive: true));
            Assert.AreEqual(BossStepOutcome.TransitionWaitingDirective, waitingA.Outcome);

            var oldCommandId = authority.LastDispatchCommandId;
            authority.InjectReceipt(new EncounterDirectiveReceipt(oldCommandId, scopeA, true, EncounterDirectiveFailureKind.None, "Old attempt success"));

            runtime.StopAttempt(new BossStopCommand(new BossId("MummyGuardian_Sandbox"), scopeA, "PlayerLeftArena"));
            runtime.PrepareReset(new BossPrepareResetCommand(new BossId("MummyGuardian_Sandbox"), scopeA));

            var scopeB = new BossAttemptScope(2, 2);
            runtime.StartAttempt(new BossStartCommand(new BossId("MummyGuardian_Sandbox"), runtime.Definition, scopeB, 2, 43, "new"));
            var waitingB = runtime.Step(World(scopeB, tick: 3, hpPercent: 60, isAlive: true));
            Assert.AreEqual(BossStepOutcome.TransitionWaitingDirective, waitingB.Outcome);
            Assert.AreEqual(new BossPhaseId("P1_Awakening"), runtime.Snapshot().CurrentPhaseId);

            var ignored = runtime.Step(World(scopeB, tick: 4, hpPercent: 60, isAlive: true));
            Assert.AreEqual(BossStepOutcome.TransitionWaitingDirective, ignored.Outcome);
            Assert.AreEqual(BossLifecycleState.Transitioning, runtime.Snapshot().Lifecycle);
            Assert.AreEqual(new BossPhaseId("P1_Awakening"), runtime.Snapshot().CurrentPhaseId);
            Assert.AreEqual(1, runtime.Snapshot().StaleDirectiveCount);
        }

        [Test]
        public void T8_Deterministic_Phase_Sequence_With_Identical_Inputs_And_Seed()
        {
            var sequence = new[]
            {
                (tick: 1L, hp: 100, facts: (IReadOnlyDictionary<string, BossWorldFact>)null, alive: true),
                (tick: 2L, hp: 69, facts: (IReadOnlyDictionary<string, BossWorldFact>)new Dictionary<string, BossWorldFact> { { "GuardianRunesComplete", new BossWorldFact("GuardianRunesComplete", false, 1) } }, alive: true),
                (tick: 3L, hp: 69, facts: (IReadOnlyDictionary<string, BossWorldFact>)new Dictionary<string, BossWorldFact> { { "GuardianRunesComplete", new BossWorldFact("GuardianRunesComplete", true, 2) } }, alive: true)
            };

            var runtimeA = BuildRuntimeWithDefinition(BossFixtureDefinition_Deterministic(), out _);
            runtimeA.StartAttempt(new BossStartCommand(new BossId("MummyGuardian_Sandbox"), runtimeA.Definition, new BossAttemptScope(3, 1), 0, 55, "base"));
            var runA = RunSequence(runtimeA, new BossAttemptScope(3, 1), sequence);

            runtimeA.StopAttempt(new BossStopCommand(new BossId("MummyGuardian_Sandbox"), new BossAttemptScope(3, 1), "Replay"));
            runtimeA.PrepareReset(new BossPrepareResetCommand(new BossId("MummyGuardian_Sandbox"), new BossAttemptScope(3, 1)));
            runtimeA.StartAttempt(new BossStartCommand(new BossId("MummyGuardian_Sandbox"), runtimeA.Definition, new BossAttemptScope(3, 2), 0, 55, "base"));
            var runAAfterReset = RunSequence(runtimeA, new BossAttemptScope(3, 2), sequence);

            var runtimeB = BuildRuntimeWithDefinition(BossFixtureDefinition_Deterministic(), out _);
            runtimeB.StartAttempt(new BossStartCommand(new BossId("MummyGuardian_Sandbox"), runtimeB.Definition, new BossAttemptScope(4, 1), 0, 55, "base"));
            var runB = RunSequence(runtimeB, new BossAttemptScope(4, 1), sequence);

            CollectionAssert.AreEqual(runA.Select(x => x.CurrentPhaseId.Value).ToList(), runB.Select(x => x.CurrentPhaseId.Value).ToList());
            CollectionAssert.AreEqual(runA.Select(x => x.TransitionLog).ToList(), runB.Select(x => x.TransitionLog).ToList());
            Assert.AreEqual(runAAfterReset.Last().CurrentPhaseId, runA.Last().CurrentPhaseId);
        }

        private static BossRuntime BuildRuntimeWithDefinition(BossDefinition definition, out FakeEncounterAuthority authority)
        {
            authority = new FakeEncounterAuthority();
            return new BossRuntime(definition, authority);
        }

        private static List<ReplayState> RunSequence(BossRuntime runtime, BossAttemptScope scope, IEnumerable<(long tick, int hp, IReadOnlyDictionary<string, BossWorldFact> facts, bool alive)> sequence)
        {
            var history = new List<ReplayState>();
            foreach (var item in sequence)
            {
                runtime.Step(World(scope, item.tick, item.hp, item.alive, item.facts));
                var snapshot = runtime.Snapshot();
                history.Add(new ReplayState(
                    snapshot.CurrentPhaseId,
                    string.Join(",", snapshot.TraceDepth, snapshot.BaselineId)));
            }

            return history;
        }

        private static BossWorldView World(
            BossAttemptScope scope,
            long tick,
            int hpPercent,
            bool isAlive,
            IReadOnlyDictionary<string, BossWorldFact> arenaFacts = null)
        {
            return new BossWorldView(
                scope,
                new HealthFraction(hpPercent),
                isAlive,
                tick,
                1,
                arenaFacts == null || arenaFacts.Count == 0 ? 1 : arenaFacts.Values.Max(f => f.Revision),
                arenaFacts ?? new Dictionary<string, BossWorldFact>());
        }

        private static BossDefinition BossFixtureDefinition_HealthPhaseFlow()
        {
            return new BossDefinition(
                new BossDefinitionId("Def-MummyGuardian-Health"),
                new BossId("MummyGuardian_Sandbox"),
                "Health transition fixture",
                new BossPhaseId("P1_Awakening"),
                new[]
                {
                    new BossPhaseDefinition(
                        new BossPhaseId("P1_Awakening"),
                        "P1",
                        new[]
                        {
                            new BossTransitionDefinition(
                                new BossTransitionId("T1_A"),
                                new BossPhaseId("P1_Awakening"),
                                new BossPhaseId("P2_Sealed"),
                                priority: 10,
                                new HealthAtOrBelowCondition(70))
                        }),
                    new BossPhaseDefinition(new BossPhaseId("P2_Sealed"), "P2", new BossTransitionDefinition[0])
                });
        }

        private static BossDefinition BossFixtureDefinition_ArenaPhaseFlow()
        {
            return new BossDefinition(
                new BossDefinitionId("Def-MummyGuardian-Arena"),
                new BossId("MummyGuardian_Sandbox"),
                "Arena transition fixture",
                new BossPhaseId("P2_Sealed"),
                new[]
                {
                    new BossPhaseDefinition(new BossPhaseId("P2_Sealed"), "P2", new[]
                    {
                        new BossTransitionDefinition(
                            new BossTransitionId("T2_A"),
                            new BossPhaseId("P2_Sealed"),
                            new BossPhaseId("P3_Unbound"),
                            10,
                            new ArenaFactEqualsCondition("GuardianRunesComplete", true, 0))
                    }),
                    new BossPhaseDefinition(new BossPhaseId("P3_Unbound"), "P3", new BossTransitionDefinition[0])
                });
        }

        private static BossDefinition BossFixtureDefinition_Simultaneous()
        {
            return new BossDefinition(
                new BossDefinitionId("Def-MummyGuardian-Simultaneous"),
                new BossId("MummyGuardian_Sandbox"),
                "Simultaneous transitions",
                new BossPhaseId("P1_Awakening"),
                new[]
                {
                    new BossPhaseDefinition(new BossPhaseId("P1_Awakening"), "P1", new[]
                    {
                        new BossTransitionDefinition(
                            new BossTransitionId("T1_A"),
                            new BossPhaseId("P1_Awakening"),
                            new BossPhaseId("P2_Sealed"),
                            1,
                            new AllCondition(
                                new HealthAtOrBelowCondition(80),
                                new ArenaFactEqualsCondition("GuardianRunesComplete", false, 0))),
                        new BossTransitionDefinition(
                            new BossTransitionId("T2_B"),
                            new BossPhaseId("P1_Awakening"),
                            new BossPhaseId("P3_Elite"),
                            2,
                            new HealthAtOrBelowCondition(70))
                    }),
                    new BossPhaseDefinition(new BossPhaseId("P2_Sealed"), "P2", new BossTransitionDefinition[0]),
                    new BossPhaseDefinition(new BossPhaseId("P3_Elite"), "P3", new BossTransitionDefinition[0])
                });
        }

        private static BossDefinition BossFixtureDefinition_WithDirective()
        {
            return new BossDefinition(
                new BossDefinitionId("Def-MummyGuardian-Directive"),
                new BossId("MummyGuardian_Sandbox"),
                "Directive fixture",
                new BossPhaseId("P1_Awakening"),
                new[]
                {
                    new BossPhaseDefinition(new BossPhaseId("P1_Awakening"), "P1", new[]
                    {
                        new BossTransitionDefinition(
                            new BossTransitionId("T1_D"),
                            new BossPhaseId("P1_Awakening"),
                            new BossPhaseId("P2_Sealed"),
                            10,
                            new HealthAtOrBelowCondition(70),
                            new EncounterDirectiveRequirement("EnableBarrier", 50))
                    }),
                    new BossPhaseDefinition(new BossPhaseId("P2_Sealed"), "P2", new BossTransitionDefinition[0])
                });
        }

        private static BossDefinition BossFixtureDefinition_Deterministic()
        {
            return new BossDefinition(
                new BossDefinitionId("Def-MummyGuardian-Deterministic"),
                new BossId("MummyGuardian_Sandbox"),
                "Deterministic fixture",
                new BossPhaseId("P1_Awakening"),
                new[]
                {
                    new BossPhaseDefinition(new BossPhaseId("P1_Awakening"), "P1", new[]
                    {
                        new BossTransitionDefinition(
                            new BossTransitionId("T1_A"),
                            new BossPhaseId("P1_Awakening"),
                            new BossPhaseId("P2_Sealed"),
                            10,
                            new HealthAtOrBelowCondition(70))
                    }),
                    new BossPhaseDefinition(new BossPhaseId("P2_Sealed"), "P2", new[]
                    {
                        new BossTransitionDefinition(
                            new BossTransitionId("T2_B"),
                            new BossPhaseId("P2_Sealed"),
                            new BossPhaseId("P3_Unbound"),
                            10,
                            new ArenaFactEqualsCondition("GuardianRunesComplete", true, 0))
                    }),
                    new BossPhaseDefinition(new BossPhaseId("P3_Unbound"), "P3", new BossTransitionDefinition[0])
                });
        }

        private readonly struct ReplayState
        {
            public BossPhaseId CurrentPhaseId { get; }
            public string TransitionLog { get; }

            public ReplayState(BossPhaseId phaseId, string transitionLog)
            {
                CurrentPhaseId = phaseId;
                TransitionLog = transitionLog;
            }
        }

        private sealed class FakeEncounterAuthority : IBossWorldPort
        {
            private readonly Dictionary<AttemptDirectiveCommandId, EncounterDirectiveReceipt> _receipts = new();
            private EncounterDirectiveRequest _lastRequest;

            public AttemptDirectiveCommandId LastDispatchCommandId => _lastRequest.CommandId;

            public EncounterDirectiveDispatchResult DispatchEncounterDirective(in EncounterDirectiveRequest request)
            {
                _lastRequest = request;
                return EncounterDirectiveDispatchResult.DispatchPending(request.CommandId);
            }

            public bool TryGetDirectiveReceipt(in AttemptDirectiveCommandId commandId, out EncounterDirectiveReceipt receipt)
            {
                if (_receipts.TryGetValue(commandId, out receipt))
                {
                    return true;
                }

                receipt = default;
                return false;
            }

            public void InjectReceipt(EncounterDirectiveReceipt receipt)
            {
                _receipts[receipt.CommandId] = receipt;
            }
        }
    }
}
