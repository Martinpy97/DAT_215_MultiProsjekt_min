using System;
using System.Collections.Generic;

namespace SparkBossFramework001.Core
{
    public sealed class PhaseElapsedAtLeastCondition : ITransitionCondition
    {
        public long Ticks { get; }
        public int EstimatedDepth => 1;
        public PhaseElapsedAtLeastCondition(long ticks) { Ticks = ticks; }
        public ConditionEvaluation Evaluate(in BossTransitionContext context)
        {
            var elapsed = context.World.Tick - context.PhaseStartTick;
            return new ConditionEvaluation(elapsed >= Ticks, $"PhaseElapsed {elapsed} >= {Ticks}");
        }
    }

    public static class BossDefinitionValidation
    {
        public const int MaxDepth = 4;
        public const int MaxConditionNodes = 32;
        public static BossRuntimeValidationResult Validate(BossDefinition definition)
        {
            if (definition.Phases.Count == 0 || definition.Phases.Count > 16
                || !definition.TryGetPhase(definition.InitialPhaseId, out _)) return Bad("Invalid phase count or initial phase");
            var phases = new HashSet<string>(StringComparer.Ordinal);
            var transitions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var phase in definition.Phases)
            {
                if (phase == null || string.IsNullOrWhiteSpace(phase.Id.Value) || !phases.Add(phase.Id.Value)) return Bad("Duplicate/invalid phase");
                if (phase.Transitions.Count > 16) return Bad("Too many transitions");
                foreach (var t in phase.Transitions)
                {
                    if (t == null || string.IsNullOrWhiteSpace(t.Id.Value) || !transitions.Add(t.Id.Value)) return Bad("Duplicate/invalid transition");
                    if (!definition.TryGetPhase(t.TargetPhaseId, out _) || t.TargetPhaseId.Equals(phase.Id)) return Bad("Missing target or forbidden self transition");
                    int nodes = 0;
                    if (!ValidCondition(t.Condition, 1, ref nodes)) return Bad("Invalid, unknown or unbounded condition");
                    if (t.RequiredDirective.HasValue && (string.IsNullOrWhiteSpace(t.RequiredDirective.Value.DirectiveType)
                        || t.RequiredDirective.Value.TimeoutTicks <= 0 || t.RequiredDirective.Value.TimeoutTicks > 1000000)) return Bad("Directive needs a finite positive timeout");
                }
            }
            var reachable = new HashSet<string>();
            var queue = new Queue<BossPhaseId>();
            queue.Enqueue(definition.InitialPhaseId);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!reachable.Add(id.Value)) continue;
                definition.TryGetPhase(id, out var phase);
                foreach (var t in phase.Transitions) queue.Enqueue(t.TargetPhaseId);
            }
            if (reachable.Count != phases.Count) return Bad("Unreachable phase");
            var visiting = new HashSet<string>();
            var visited = new HashSet<string>();
            foreach (var phase in definition.Phases)
                if (HasUnconditionalCycle(definition, phase, visiting, visited)) return Bad("Unconditional cycle");
            return new BossRuntimeValidationResult(true, "Definition valid");
        }

        private static bool ValidCondition(ITransitionCondition condition, int depth, ref int nodes)
        {
            if (condition == null || depth > MaxDepth || ++nodes > MaxConditionNodes) return false;
            if (condition is HealthAtOrBelowCondition) return true;
            if (condition is ArenaFactEqualsCondition fact) return !string.IsNullOrWhiteSpace(fact.FactId) && fact.RevisionPolicy >= 0;
            if (condition is PhaseElapsedAtLeastCondition timer) return timer.Ticks >= 0;
            var children = condition is AllCondition all ? all.Children : (condition as AnyCondition)?.Children;
            if (children == null || children.Count == 0) return false;
            foreach (var child in children) if (!ValidCondition(child, depth + 1, ref nodes)) return false;
            return true;
        }

        private static bool Always(ITransitionCondition c)
        {
            if (c is HealthAtOrBelowCondition hp) return hp.Threshold.NormalizedValue == 10000;
            if (c is PhaseElapsedAtLeastCondition timer) return timer.Ticks == 0;
            if (c is AllCondition all) { foreach (var child in all.Children) if (!Always(child)) return false; return true; }
            if (c is AnyCondition any) { foreach (var child in any.Children) if (Always(child)) return true; }
            return false;
        }

        private static bool HasUnconditionalCycle(BossDefinition d, BossPhaseDefinition phase, HashSet<string> visiting, HashSet<string> visited)
        {
            if (visiting.Contains(phase.Id.Value)) return true;
            if (!visited.Add(phase.Id.Value)) return false;
            visiting.Add(phase.Id.Value);
            foreach (var t in phase.Transitions)
            {
                if (!Always(t.Condition)) continue;
                d.TryGetPhase(t.TargetPhaseId, out var target);
                if (HasUnconditionalCycle(d, target, visiting, visited)) return true;
            }
            visiting.Remove(phase.Id.Value);
            return false;
        }
        private static BossRuntimeValidationResult Bad(string reason) => new BossRuntimeValidationResult(false, reason);
    }
}
