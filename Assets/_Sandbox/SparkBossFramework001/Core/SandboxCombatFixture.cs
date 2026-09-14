using SparkBossFramework001.Core;

namespace SparkBossFramework001.Sandbox
{
    public enum SandboxHit { Hit, Defeated, RangedShield, RuneShield, StaleScope, Inactive }

    // Fake encounter rules for the playable demonstration, not production Combat.
    public sealed class SandboxCombatFixture
    {
        private readonly FakeEncounterAuthority _authority;
        private BossAttemptScope _scope;
        public int AddHealth { get; private set; }
        public bool RangedImmune => AddHealth > 0;
        public SandboxCombatFixture(FakeEncounterAuthority authority) { _authority = authority; Reset(); }
        public void Reset() { _scope = _authority.Scope; AddHealth = 40; }
        private bool Current(BossAttemptScope scope) => scope.Equals(_scope) && scope.Equals(_authority.Scope);
        private bool Active => _authority.Runtime.Snapshot().Lifecycle == BossLifecycleState.Active;

        public SandboxHit HitAdd(BossAttemptScope scope, int damage)
        {
            if (!Current(scope)) return SandboxHit.StaleScope;
            if (!Active || damage <= 0 || AddHealth == 0) return SandboxHit.Inactive;
            AddHealth = System.Math.Max(0, AddHealth - damage);
            return AddHealth == 0 ? SandboxHit.Defeated : SandboxHit.Hit;
        }

        public SandboxHit HitBoss(BossAttemptScope scope, int damage, bool ranged)
        {
            if (!Current(scope)) return SandboxHit.StaleScope;
            if (!Active || damage <= 0) return SandboxHit.Inactive;
            if (_authority.Runtime.Snapshot().CurrentPhaseId.Equals(FakeEncounterAuthority.P2)) return SandboxHit.RuneShield;
            if (ranged && RangedImmune) return SandboxHit.RangedShield;
            int hp = System.Math.Max(0, _authority.HealthPercent - damage);
            _authority.SetHealth(hp, hp > 0);
            _authority.Step();
            return hp == 0 ? SandboxHit.Defeated : SandboxHit.Hit;
        }
    }
}
