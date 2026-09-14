using NUnit.Framework;
using SparkBossFramework001.Sandbox;

namespace SparkBossFramework001.Tests.EditMode
{
    public sealed class SandboxCombatFixtureTests
    {
        [Test] public void LivingAdd_BlocksRanged_ButAllowsMelee()
        {
            var f = new FakeEncounterAuthority(); f.Start(); var c = new SandboxCombatFixture(f);
            Assert.AreEqual(SandboxHit.RangedShield,c.HitBoss(f.Scope,15,true));
            Assert.AreEqual(100,f.HealthPercent);
            Assert.AreEqual(SandboxHit.Hit,c.HitBoss(f.Scope,10,false));
            Assert.AreEqual(90,f.HealthPercent);
        }
        [Test] public void KillingAdd_ImmediatelyRemovesRangedImmunity()
        {
            var f = new FakeEncounterAuthority(); f.Start(); var c = new SandboxCombatFixture(f);
            Assert.AreEqual(SandboxHit.Hit,c.HitAdd(f.Scope,20));
            Assert.IsTrue(c.RangedImmune);
            Assert.AreEqual(SandboxHit.Defeated,c.HitAdd(f.Scope,20));
            Assert.IsFalse(c.RangedImmune);
            Assert.AreEqual(SandboxHit.Hit,c.HitBoss(f.Scope,15,true));
            Assert.AreEqual(85,f.HealthPercent);
            Assert.AreEqual(SandboxHit.Inactive,c.HitAdd(f.Scope,20));
        }
        [Test] public void RuneShield_RemainsSeparate_FromAddShield()
        {
            var f = new FakeEncounterAuthority(); f.Start(); var c = new SandboxCombatFixture(f);
            c.HitAdd(f.Scope,40); c.HitBoss(f.Scope,30,false);
            Assert.AreEqual(SandboxHit.RuneShield,c.HitBoss(f.Scope,15,true));
            Assert.AreEqual(SandboxHit.RuneShield,c.HitBoss(f.Scope,10,false));
            Assert.AreEqual(70,f.HealthPercent);
            f.SetRunes(true); f.Step();
            Assert.AreEqual(SandboxHit.Hit,c.HitBoss(f.Scope,15,true));
            Assert.AreEqual(55,f.HealthPercent);
        }
        [Test] public void Reset_RestoresAdd_AndRejectsOldProjectiles()
        {
            var f = new FakeEncounterAuthority(); f.Start(); var c = new SandboxCombatFixture(f);
            var old=f.Scope;c.HitAdd(old,40);f.Reset();c.Reset();
            Assert.AreEqual(40,c.AddHealth);Assert.IsTrue(c.RangedImmune);
            Assert.AreEqual(SandboxHit.StaleScope,c.HitAdd(old,40));
            Assert.AreEqual(SandboxHit.StaleScope,c.HitBoss(old,100,true));
            Assert.AreEqual(100,f.HealthPercent);Assert.AreEqual(40,c.AddHealth);
        }
    }
}
