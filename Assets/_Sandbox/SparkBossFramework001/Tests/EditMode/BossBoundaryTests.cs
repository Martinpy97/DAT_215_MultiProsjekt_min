using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SparkBossFramework001.Core;
using SparkBossFramework001.Sandbox;

namespace SparkBossFramework001.Tests.EditMode
{
    public sealed class BossBoundaryTests
    {
        private static readonly BossPhaseId A = FakeEncounterAuthority.P1;
        private static readonly BossPhaseId B = FakeEncounterAuthority.P2;
        private static readonly BossPhaseId C = FakeEncounterAuthority.P3;
        private static FakeEncounterAuthority Start(BossDefinition def = null, bool auto = true)
        {
            var f = new FakeEncounterAuthority(def) { AutoAcknowledge = auto };
            f.Start(123);
            return f;
        }
        private static void Pending(FakeEncounterAuthority f)
        {
            f.SetHealth(70); Assert.AreEqual(BossStepOutcome.TransitionCommitted, f.Step().Outcome);
            f.SetRunes(true); Assert.AreEqual(BossStepOutcome.TransitionWaitingDirective, f.Step().Outcome);
        }
        private static BossDefinition Two(ITransitionCondition condition, BossPhaseId? target = null)
        {
            return new BossDefinition(new BossDefinitionId("two"), new BossId("test"), "test", A, new[] {
                new BossPhaseDefinition(A, "A", new[] { new BossTransitionDefinition(new BossTransitionId("AB"), A, target ?? B, 1, condition) }),
                new BossPhaseDefinition(B, "B", Array.Empty<BossTransitionDefinition>()) });
        }

        [Test] public void Reset_AfterPlayerDeath_HandlesAlreadyStoppedAttempt()
        {
            var f = Start();
            var old = f.Scope;
            f.Runtime.StopAttempt(new BossStopCommand(f.Runtime.Definition.BossId, old, "SandboxPlayerDied"));
            Assert.DoesNotThrow(() => f.Reset());
            Assert.AreEqual(BossLifecycleState.Active, f.Runtime.Snapshot().Lifecycle);
            Assert.AreEqual(A, f.Runtime.Snapshot().CurrentPhaseId);
            Assert.Greater(f.Scope.Epoch, old.Epoch);
            Assert.AreEqual(100, f.HealthPercent);
        }

        [Test] public void T1_InclusiveThreshold_And_Reason_Recorded()
        {
            var f = Start(); f.SetHealth(71); Assert.AreEqual(BossStepOutcome.NoChange, f.Step().Outcome);
            f.SetHealth(70); Assert.AreEqual(BossStepOutcome.TransitionCommitted, f.Step().Outcome);
            StringAssert.Contains("70%", f.Runtime.Trace.Single().ConditionTrace);
            f.SetHealth(100); f.Step(); Assert.AreEqual(B, f.Runtime.Snapshot().CurrentPhaseId);
        }
        [Test] public void T3_EqualPriority_Uses_DefinitionOrder()
        {
            var def = new BossDefinition(new BossDefinitionId("tie"), new BossId("test"), "tie", A, new[] {
                new BossPhaseDefinition(A,"A",new[] {
                    new BossTransitionDefinition(new BossTransitionId("first"),A,B,7,new HealthAtOrBelowCondition(70)),
                    new BossTransitionDefinition(new BossTransitionId("second"),A,C,7,new HealthAtOrBelowCondition(70)) }),
                new BossPhaseDefinition(B,"B",Array.Empty<BossTransitionDefinition>()),
                new BossPhaseDefinition(C,"C",Array.Empty<BossTransitionDefinition>()) });
            var f = Start(def); f.SetHealth(60); f.Step();
            Assert.AreEqual(B, f.Runtime.Snapshot().CurrentPhaseId);
            Assert.AreEqual("first", f.Runtime.Trace.Single().TransitionId.Value);
        }
        [Test] public void T4_Timeout_StaysInPhase_And_BoundsRetries()
        {
            var f = Start(auto:false); Pending(f);
            for (int i=0; i<BossRuntime.MaxDirectiveAttempts; i++)
            {
                if(i>0) Assert.AreEqual(BossStepOutcome.TransitionWaitingDirective,f.Step().Outcome);
                var result=f.Step(5);
                Assert.AreEqual(BossStepOutcome.TransitionTimeout,result.Outcome);
                Assert.AreEqual(BossTransitionFailureKind.DirectiveTimeout,result.FailureKind);
                Assert.AreEqual(B,f.Runtime.Snapshot().CurrentPhaseId);
            }
            for(int i=0;i<100;i++) f.Step();
            Assert.AreEqual(BossRuntime.MaxDirectiveAttempts,f.DispatchCount);
            Assert.LessOrEqual(f.Runtime.Trace.Count,BossRuntime.TraceCapacity);
        }
        [Test] public void T4_Receipt_Commits_At_CurrentWorldTick_Once()
        {
            var f=Start(auto:false); Pending(f);
            var receipt=new EncounterDirectiveReceipt(f.LastRequest.CommandId,f.Scope,true,EncounterDirectiveFailureKind.None,"");
            f.Runtime.SubmitDirectiveReceipt(receipt);
            Assert.AreEqual(B,f.Runtime.Snapshot().CurrentPhaseId);
            Assert.AreEqual(BossStepOutcome.TransitionCommitted,f.Step().Outcome);
            Assert.AreEqual(f.Tick,f.Runtime.Snapshot().PhaseStartTick);
            Assert.AreEqual(C,f.Runtime.Snapshot().CurrentPhaseId);
            Assert.AreEqual(BossReceiptSubmissionOutcome.NotWaiting,f.Runtime.SubmitDirectiveReceipt(receipt).Outcome);
        }
        [Test] public void T5_Death_Beats_QueuedSuccessfulReceipt()
        {
            var f=Start(auto:false); Pending(f);
            f.Runtime.SubmitDirectiveReceipt(new EncounterDirectiveReceipt(f.LastRequest.CommandId,f.Scope,true,EncounterDirectiveFailureKind.None,""));
            f.SetHealth(0,false);
            Assert.AreEqual(BossStepOutcome.Defeated,f.Step().Outcome);
            Assert.AreEqual(B,f.Runtime.Snapshot().CurrentPhaseId);
        }
        [Test] public void T6_Authority_RestoresWorld_And_AllocatesNewScope_AfterResetReady()
        {
            var f=Start(); f.SetHealth(65);f.Step();f.SetRunes(true);f.Step();
            var old=f.Scope; f.Reset(987);
            Assert.AreEqual(BossLifecycleState.ResetReady,f.LastResetReady.Lifecycle);
            Assert.AreEqual(old,f.LastResetReady.AttemptScope);
            Assert.AreEqual(0,f.LastResetReady.TraceDepth);
            Assert.AreEqual(0,f.LastResetReady.DecisionSeed);
            Assert.Greater(f.Scope.Epoch,old.Epoch);
            Assert.AreEqual(100,f.HealthPercent);Assert.IsFalse(f.RunesComplete);
            var s=f.Runtime.Snapshot();Assert.AreEqual(A,s.CurrentPhaseId);Assert.AreEqual(987,s.DecisionSeed);
            Assert.AreEqual(0,s.TraceDepth);Assert.IsTrue(f.AdmissionsOpen);
        }
        [Test] public void T7_StaleFact_And_DirectReceipt_Preserve_NewPendingTransition()
        {
            var f=Start(auto:false);Pending(f);
            var old=f.Capture();var receipt=new EncounterDirectiveReceipt(f.LastRequest.CommandId,f.Scope,true,EncounterDirectiveFailureKind.None,"");
            f.Reset();Pending(f);var before=f.Runtime.Snapshot();
            Assert.AreEqual(BossStepOutcome.StaleAttemptInput,f.Runtime.Step(old).Outcome);
            Assert.AreEqual(BossReceiptSubmissionOutcome.RejectedStale,f.Runtime.SubmitDirectiveReceipt(receipt).Outcome);
            var after=f.Runtime.Snapshot();Assert.AreEqual(before.CurrentPhaseId,after.CurrentPhaseId);
            Assert.AreEqual(before.Lifecycle,after.Lifecycle);Assert.AreEqual(before.LastWorldTick,after.LastWorldTick);
            Assert.AreEqual(before.TraceDepth,after.TraceDepth);
            f.Receipt(true);Assert.AreEqual(BossStepOutcome.TransitionCommitted,f.Step().Outcome);
        }
        [Test] public void T8_FullSemanticTrace_IsIdentical_AfterAuthorityReset()
        {
            var f=Start();var first=Replay(f);f.Reset(123);var second=Replay(f);
            CollectionAssert.AreEqual(first,second);Assert.AreEqual(123,f.Runtime.Snapshot().DecisionSeed);
        }
        private static string[] Replay(FakeEncounterAuthority f)
        {
            f.Step();f.SetHealth(70);f.Step();f.SetRunes(true);f.Step();
            return f.Runtime.Trace.Select(t=>$"{t.TransitionId}|{t.From}|{t.To}|{t.ConditionTrace}|{t.Outcome}|{t.FailureKind}|{t.Tick}|{t.Notes}").ToArray();
        }
        [Test] public void Timer_All_Any_Are_Evaluated_From_PhaseStart()
        {
            var condition=new AllCondition(new PhaseElapsedAtLeastCondition(3),new AnyCondition(new HealthAtOrBelowCondition(70),new ArenaFactEqualsCondition("GuardianRunesComplete",true)));
            var f=Start(Two(condition));f.SetHealth(65);
            Assert.AreEqual(BossStepOutcome.NoChange,f.Step(2).Outcome);
            Assert.AreEqual(BossStepOutcome.TransitionCommitted,f.Step().Outcome);
        }
        [Test] public void InvalidDefinitions_AreRejected_BeforeStart()
        {
            Assert.IsFalse(new FakeEncounterAuthority(Two(new HealthAtOrBelowCondition(70),new BossPhaseId("missing"))).Runtime.IsValid);
            Assert.IsFalse(new FakeEncounterAuthority(Two(new HealthAtOrBelowCondition(70),A)).Runtime.IsValid);
            Assert.IsFalse(new FakeEncounterAuthority(Two(new AllCondition())).Runtime.IsValid);
            ITransitionCondition deep=new HealthAtOrBelowCondition(70);
            for(int i=0;i<5;i++)deep=new AllCondition(deep);
            Assert.IsFalse(new FakeEncounterAuthority(Two(deep)).Runtime.IsValid);
            Assert.IsFalse(new FakeEncounterAuthority(Two(new AnyCondition(Enumerable.Range(0,33).Select(_=>(ITransitionCondition)new HealthAtOrBelowCondition(70)).ToArray()))).Runtime.IsValid);
        }
        [Test] public void OldTick_IsRejected_WithoutRewindingPhaseClock()
        {
            var f=Start();f.Step(10);var before=f.Runtime.Snapshot();
            Assert.AreEqual(BossStepOutcome.StaleAttemptInput,f.Runtime.Step(f.Capture(2)).Outcome);
            Assert.AreEqual(before.LastWorldTick,f.Runtime.Snapshot().LastWorldTick);
        }
        [Test] public void ReusingInvalidatedEpoch_IsRejected()
        {
            var f=Start();var scope=f.Scope;var boss=f.Runtime.Definition.BossId;
            f.Runtime.StopAttempt(new BossStopCommand(boss,scope,"stop"));
            f.Runtime.PrepareReset(new BossPrepareResetCommand(boss,scope));
            Assert.AreEqual(BossCommandOutcome.StaleAttempt,f.Runtime.StartAttempt(new BossStartCommand(boss,f.Runtime.Definition,scope,0,123,"baseline")).Outcome);
        }
        [Test] public void WorldProjection_And_DefinitionCollections_AreReadOnly()
        {
            var f=Start();var facts=new Dictionary<string,BossWorldFact>{{"GuardianRunesComplete",new BossWorldFact("GuardianRunesComplete",false,1)}};
            var world=new BossWorldView(f.Scope,new HealthFraction(100),true,1,1,1,facts);
            facts["GuardianRunesComplete"]=new BossWorldFact("GuardianRunesComplete",true,2);
            Assert.IsFalse(world.ArenaFacts["GuardianRunesComplete"].Value);
            Assert.IsTrue(((IList<BossPhaseDefinition>)f.Runtime.Definition.Phases).IsReadOnly);
            Assert.IsTrue(((IList<BossRuntimeTrace>)f.Runtime.Trace).IsReadOnly);
        }
    }
}
