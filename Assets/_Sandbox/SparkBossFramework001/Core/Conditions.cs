using System.Collections.Generic;
using System.Text;

namespace SparkBossFramework001.Core
{
    public readonly struct ConditionEvaluation
    {
        public bool IsSatisfied { get; }
        public string Trace { get; }

        public ConditionEvaluation(bool isSatisfied, string trace)
        {
            IsSatisfied = isSatisfied;
            Trace = trace;
        }
    }

    public readonly struct BossTransitionContext
    {
        public BossWorldView World { get; }
        public BossPhaseId SourcePhaseId { get; }
        public long PhaseStartTick { get; }

        public BossTransitionContext(in BossWorldView world, BossPhaseId sourcePhaseId, long phaseStartTick = 0)
        {
            World = world;
            SourcePhaseId = sourcePhaseId;
            PhaseStartTick = phaseStartTick;
        }
    }

    public interface ITransitionCondition
    {
        ConditionEvaluation Evaluate(in BossTransitionContext context);
        int EstimatedDepth { get; }
    }

    public sealed class HealthAtOrBelowCondition : ITransitionCondition
    {
        public HealthFraction Threshold { get; }
        public HealthAtOrBelowCondition(int percent)
        {
            Threshold = new HealthFraction(percent);
        }

        public ConditionEvaluation Evaluate(in BossTransitionContext context)
        {
            bool ok = context.World.BossHealth <= Threshold;
            var detail = $"Health {context.World.BossHealth} <= {Threshold}";
            return new ConditionEvaluation(ok, detail);
        }

        public int EstimatedDepth => 1;
    }

    public sealed class ArenaFactEqualsCondition : ITransitionCondition
    {
        public string FactId { get; }
        public bool Expected { get; }
        public long RevisionPolicy { get; }

        public ArenaFactEqualsCondition(string factId, bool expected, long revisionPolicy = 0)
        {
            FactId = factId;
            Expected = expected;
            RevisionPolicy = revisionPolicy;
        }

        public ConditionEvaluation Evaluate(in BossTransitionContext context)
        {
            if (context.World.ArenaFacts == null || !context.World.ArenaFacts.TryGetValue(FactId, out var fact))
            {
                return new ConditionEvaluation(false, $"ArenaFact({FactId})=MISSING");
            }

            if (fact.FactId != FactId || fact.Revision < RevisionPolicy || fact.Revision > context.World.ArenaRevision)
            {
                return new ConditionEvaluation(false, $"ArenaFact({FactId}) revision {fact.Revision} < policy {RevisionPolicy}");
            }

            bool ok = fact.Value == Expected;
            string trace = $"ArenaFact({FactId})={fact.Value} (rev {fact.Revision}) == {Expected}";
            return new ConditionEvaluation(ok, trace);
        }

        public int EstimatedDepth => 1;
    }

    public sealed class AllCondition : ITransitionCondition
    {
        private readonly IReadOnlyList<ITransitionCondition> _children;
        public IReadOnlyList<ITransitionCondition> Children => _children;

        public AllCondition(params ITransitionCondition[] children)
        {
            _children = System.Array.AsReadOnly((ITransitionCondition[])(children ?? new ITransitionCondition[0]).Clone());
        }

        public int EstimatedDepth
        {
            get
            {
                var maxDepth = 0;
                for (var i = 0; i < _children.Count; i++)
                {
                    maxDepth = System.Math.Max(maxDepth, _children[i].EstimatedDepth);
                }

                return maxDepth + 1;
            }
        }

        public ConditionEvaluation Evaluate(in BossTransitionContext context)
        {
            var details = new StringBuilder();
            for (var i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                var childEval = child.Evaluate(in context);
                details.Append(childEval.Trace);
                if (i + 1 < _children.Count)
                {
                    details.Append(" AND ");
                }

                if (!childEval.IsSatisfied)
                {
                    return new ConditionEvaluation(false, $"All({details})");
                }
            }

            return new ConditionEvaluation(true, $"All({details})");
        }
    }

    public sealed class AnyCondition : ITransitionCondition
    {
        private readonly IReadOnlyList<ITransitionCondition> _children;
        public IReadOnlyList<ITransitionCondition> Children => _children;

        public AnyCondition(params ITransitionCondition[] children)
        {
            _children = System.Array.AsReadOnly((ITransitionCondition[])(children ?? new ITransitionCondition[0]).Clone());
        }

        public int EstimatedDepth
        {
            get
            {
                var maxDepth = 0;
                for (var i = 0; i < _children.Count; i++)
                {
                    maxDepth = System.Math.Max(maxDepth, _children[i].EstimatedDepth);
                }

                return maxDepth + 1;
            }
        }

        public ConditionEvaluation Evaluate(in BossTransitionContext context)
        {
            var details = new StringBuilder();
            for (var i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                var childEval = child.Evaluate(in context);
                details.Append(childEval.Trace);
                if (i + 1 < _children.Count)
                {
                    details.Append(" OR ");
                }

                if (childEval.IsSatisfied)
                {
                    return new ConditionEvaluation(true, $"Any({details})");
                }
            }

            return new ConditionEvaluation(false, $"Any({details})");
        }
    }
}
