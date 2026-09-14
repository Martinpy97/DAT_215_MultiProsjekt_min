using System.Collections;
using System.Collections.Generic;
using SparkBossFramework001.Core;
using SparkBossFramework001.Sandbox;
using UnityEngine;

namespace SparkBossFramework001.Unity
{
    public sealed class BossSandboxRunner : MonoBehaviour
    {
        public Renderer bossVisual;
        public bool automaticDemo = true;
        public FakeEncounterAuthority Authority { get; private set; }
        public bool DemoComplete { get; private set; }
        public bool DemoPassed { get; private set; }
        public string LastResult { get; private set; }
        public string DemoSequence => string.Join(" -> ", _sequence);
        private readonly List<string> _sequence = new();
        private BossWorldView _oldWorld;
        private EncounterDirectiveReceipt _oldReceipt;
        private MaterialPropertyBlock _colors;

        private IEnumerator Start()
        {
            Authority = new FakeEncounterAuthority();
            Authority.Start(123);
            Record("P1", "Started attempt A");
            if (!automaticDemo) yield break;
            bool passed = Authority.Runtime.Snapshot().CurrentPhaseId.Equals(FakeEncounterAuthority.P1);
            yield return new WaitForSecondsRealtime(1);
            Authority.SetHealth(65);
            var p2 = Authority.Step();
            passed &= p2.Outcome == BossStepOutcome.TransitionCommitted && Authority.Runtime.Snapshot().CurrentPhaseId.Equals(FakeEncounterAuthority.P2);
            Record("P2", p2.Message);
            yield return new WaitForSecondsRealtime(1);
            Authority.SetRunes(true);
            _oldWorld = Authority.Capture();
            var p3 = Authority.Step();
            passed &= p3.Outcome == BossStepOutcome.TransitionCommitted && Authority.Runtime.Snapshot().CurrentPhaseId.Equals(FakeEncounterAuthority.P3);
            _oldReceipt = new EncounterDirectiveReceipt(Authority.LastRequest.CommandId, Authority.Scope, true, EncounterDirectiveFailureKind.None, "Late A receipt");
            Record("P3", p3.Message);
            yield return new WaitForSecondsRealtime(1);
            var oldScope = Authority.Scope;
            Authority.Reset(123);
            passed &= !oldScope.Equals(Authority.Scope) && Authority.HealthPercent == 100 && !Authority.RunesComplete
                && Authority.Runtime.Snapshot().CurrentPhaseId.Equals(FakeEncounterAuthority.P1);
            Record("P1 (B)", "ResetReady, restored baseline, new attempt B");
            yield return new WaitForSecondsRealtime(1);
            var staleFact = Authority.Runtime.Step(_oldWorld);
            var staleReceipt = Authority.Runtime.SubmitDirectiveReceipt(_oldReceipt);
            passed &= staleFact.Outcome == BossStepOutcome.StaleAttemptInput && staleReceipt.Outcome == BossReceiptSubmissionOutcome.RejectedStale
                && Authority.Runtime.Snapshot().CurrentPhaseId.Equals(FakeEncounterAuthority.P1);
            Record("stale rejected", "Old fact and receipt rejected; B remains P1");
            DemoPassed = passed;
            DemoComplete = true;
            Debug.Log("SPARK_BOSS_001_DEMO " + (passed ? "PASS " : "FAIL ") + DemoSequence);
        }

        private void Record(string phase, string result)
        {
            _sequence.Add(phase);
            LastResult = result;
            UpdateVisual();
        }
        private void UpdateVisual()
        {
            if (bossVisual == null || Authority == null) return;
            var phase = Authority.Runtime.Snapshot().CurrentPhaseId;
            var color = phase.Equals(FakeEncounterAuthority.P1) ? new Color(0.15f, 0.7f, 0.9f)
                : phase.Equals(FakeEncounterAuthority.P2) ? new Color(1f, 0.65f, 0.1f) : new Color(0.95f, 0.2f, 0.12f);
            if (_colors == null) _colors = new MaterialPropertyBlock();
            _colors.SetColor("_BaseColor", color);
            _colors.SetColor("_Color", color);
            bossVisual.SetPropertyBlock(_colors);
        }

        private void OnGUI()
        {
            if (!automaticDemo) return;
            if (Authority == null) return;
            var s = Authority.Runtime.Snapshot();
            float width = Mathf.Min(Screen.width - 24, 660);
            GUI.Box(new Rect(12, 12, width, 282), "BOSS-001 / ISOLATED SANDBOX");
            string text = "Boss: " + s.BossId + "\nAttempt: " + s.AttemptScope + " / seed " + s.DecisionSeed
                + "\nPhase: " + s.CurrentPhaseId + " / " + s.Lifecycle + "\nFake HP: " + Authority.HealthPercent + "% / runes: " + Authority.RunesComplete
                + "\nResult: " + LastResult + "\nRejected old facts: " + s.StaleInputCount + " / receipts: " + s.StaleDirectiveCount
                + "\nDemo: " + DemoSequence + "\nStatus: " + (DemoComplete ? (DemoPassed ? "PASS" : "FAIL") : "running");
            GUI.Label(new Rect(24, 42, width - 24, 205), text);
            GUI.enabled = DemoComplete;
            float buttonWidth = (width - 36) / 4;
            if (GUI.Button(new Rect(24, 252, buttonWidth, 28), "HP 65%")) { Authority.SetHealth(65); LastResult = Authority.Step().Message; }
            if (GUI.Button(new Rect(30 + buttonWidth, 252, buttonWidth, 28), "Runes true")) { Authority.SetRunes(true); LastResult = Authority.Step().Message; }
            if (GUI.Button(new Rect(36 + buttonWidth * 2, 252, buttonWidth, 28), "Reset")) { LastResult = Authority.Reset(123).Message; }
            if (GUI.Button(new Rect(42 + buttonWidth * 3, 252, buttonWidth, 28), "Old inputs")) { Authority.Runtime.Step(_oldWorld); LastResult = Authority.Runtime.SubmitDirectiveReceipt(_oldReceipt).Message; }
            GUI.enabled = true;
            UpdateVisual();
        }
    }
}
