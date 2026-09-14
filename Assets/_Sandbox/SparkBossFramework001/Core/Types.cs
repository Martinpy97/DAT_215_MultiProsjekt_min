using System;

namespace SparkBossFramework001.Core
{
    public enum BossLifecycleState
    {
        Unbound,
        Inactive,
        Prepared,
        Active,
        Transitioning,
        ResetReady,
        Defeated,
        Aborting,
        Stopped
    }

    public enum BossCommandOutcome
    {
        Success,
        Duplicate,
        Busy,
        StaleAttempt,
        InvalidRequest,
        MissingDefinition,
        NotReady
    }

    public enum BossStepOutcome
    {
        NoChange,
        TransitionCommitted,
        TransitionWaitingDirective,
        TransitionFailed,
        TransitionTimeout,
        Defeated,
        StaleAttemptInput,
        NotInAttempt
    }

    public enum BossTransitionFailureKind
    {
        None,
        DirectiveRejected,
        DirectiveFailed,
        DirectiveTimeout,
        ScopeInvalid,
        StaleInput,
        Unknown
    }

    public enum EncounterDirectiveFailureKind
    {
        None,
        Busy,
        Timeout,
        Rejected,
        Unknown
    }

    public readonly struct BossId : IEquatable<BossId>
    {
        public const int MaxIdLength = 128;
        public string Value { get; }

        public BossId(string value)
        {
            Value = Normalize(value, "Boss-Undefined");
        }

        public bool Equals(BossId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is BossId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode(StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return Value;
        }

        private static string Normalize(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;

            var normalized = value.Trim();
            return normalized.Length > MaxIdLength
                ? normalized.Substring(0, MaxIdLength)
                : normalized;
        }
    }

    public readonly struct BossDefinitionId : IEquatable<BossDefinitionId>
    {
        public const int MaxIdLength = 128;
        public string Value { get; }

        public BossDefinitionId(string value)
        {
            Value = Normalize(value, "Definition-Undefined");
        }

        public bool Equals(BossDefinitionId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is BossDefinitionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode(StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return Value;
        }

        private static string Normalize(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;

            var normalized = value.Trim();
            return normalized.Length > MaxIdLength
                ? normalized.Substring(0, MaxIdLength)
                : normalized;
        }
    }

    public readonly struct BossPhaseId : IEquatable<BossPhaseId>
    {
        public const int MaxIdLength = 128;
        public string Value { get; }

        public BossPhaseId(string value)
        {
            Value = Normalize(value, "Phase-Undefined");
        }

        public bool Equals(BossPhaseId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is BossPhaseId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode(StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return Value;
        }

        private static string Normalize(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;

            var normalized = value.Trim();
            return normalized.Length > MaxIdLength
                ? normalized.Substring(0, MaxIdLength)
                : normalized;
        }
    }

    public readonly struct BossTransitionId : IEquatable<BossTransitionId>
    {
        public const int MaxIdLength = 128;
        public string Value { get; }

        public BossTransitionId(string value)
        {
            Value = Normalize(value, "Transition-Undefined");
        }

        public bool Equals(BossTransitionId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is BossTransitionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode(StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return Value;
        }

        private static string Normalize(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;

            var normalized = value.Trim();
            return normalized.Length > MaxIdLength
                ? normalized.Substring(0, MaxIdLength)
                : normalized;
        }
    }

    public readonly struct AttemptDirectiveCommandId : IEquatable<AttemptDirectiveCommandId>
    {
        public ulong Value { get; }

        public AttemptDirectiveCommandId(ulong value)
        {
            Value = value;
        }

        public bool Equals(AttemptDirectiveCommandId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is AttemptDirectiveCommandId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public readonly struct BossAttemptScope : IEquatable<BossAttemptScope>
    {
        public long WorldId { get; }
        public long Epoch { get; }

        public BossAttemptScope(long worldId, long epoch)
        {
            WorldId = worldId;
            Epoch = epoch;
        }

        public bool Equals(BossAttemptScope other)
        {
            return WorldId == other.WorldId && Epoch == other.Epoch;
        }

        public override bool Equals(object obj)
        {
            return obj is BossAttemptScope other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(WorldId, Epoch);
        }

        public override string ToString()
        {
            return $"world={WorldId},epoch={Epoch}";
        }
    }

    public readonly struct HealthFraction : IEquatable<HealthFraction>
    {
        private readonly int _value;

        public HealthFraction(int percent)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            _value = percent * 100;
        }

        public static HealthFraction FromNormalized(int normalized)
        {
            return new HealthFraction(NormalizeNormalized(normalized), true);
        }

        private HealthFraction(int normalized, bool unused)
        {
            _value = NormalizeNormalized(normalized);
        }

        public int NormalizedValue => _value;

        public int Percentage => (_value + 50) / 100;

        public bool IsDead => _value <= 0;

        public bool Equals(HealthFraction other)
        {
            return _value == other._value;
        }

        public override bool Equals(object obj)
        {
            return obj is HealthFraction other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _value;
        }

        public override string ToString()
        {
            return $"{Percentage}%";
        }

        public static bool operator <=(HealthFraction left, HealthFraction right) => left._value <= right._value;

        public static bool operator >=(HealthFraction left, HealthFraction right) => left._value >= right._value;

        public static bool operator <(HealthFraction left, HealthFraction right) => left._value < right._value;

        public static bool operator >(HealthFraction left, HealthFraction right) => left._value > right._value;

        private static int NormalizeNormalized(int value)
        {
            if (value < 0) return 0;
            if (value > 10000) return 10000;
            return value;
        }
    }
}
