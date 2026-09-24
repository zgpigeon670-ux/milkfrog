using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Milkfrog.CombatDemo.Tests
{
    public class MvpBossCombatTests
    {
        MvpWorld world;
        string directory;

        [UnitySetUp]
        public IEnumerator LoadMvpLevel()
        {
            directory = Path.Combine(Path.GetTempPath(), "MilkfrogBossTest-" + Guid.NewGuid().ToString("N"));
            GameFlowController.SaveDirectoryOverride = directory;
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/CombatDemo/Scenes/MVP_TestLevel.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("MVP_TestLevel");
#endif
            yield return null;
            world = UnityEngine.Object.FindAnyObjectByType<MvpWorld>();
            world.manualSimulation = true;
            if (world.Flow.Paused) world.Flow.TogglePause();
            world.feedback.hitStop = false;
        }

        [UnityTearDown]
        public IEnumerator UnloadMvpLevel()
        {
            if (world != null)
            {
                var scene = SceneManager.GetActiveScene();
                var empty = SceneManager.CreateScene("MvpBossTestCleanup-" + Guid.NewGuid());
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            GameFlowController.SaveDirectoryOverride = null;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        static void Place(CombatActor actor, Vector3 position)
        {
            actor.Motor.enabled = false;
            actor.transform.position = position;
            actor.Motor.enabled = true;
            Physics.SyncTransforms();
        }

        EnemyController EngageBoss()
        {
            var boss = world.enemies[3];
            Place(world.player, boss.Home + Vector3.back * 1f);
            world.Director.Tick(.01f);
            Assert.That(world.Director.State, Is.EqualTo(EncounterState.BossCombat));
            Assert.That(boss.Actor.AcceptsDamage, Is.True);
            return boss;
        }

        static void BeginBossAttack(EnemyController boss, CombatState phase)
        {
            boss.Actor.Core.Reset();
            var attack = boss.Actor.lightAttack.rules;
            Assert.That(boss.Actor.Core.RequestDefinedAttack(attack), Is.True);
            if (phase == CombatState.AttackActive) boss.Actor.Core.Tick(attack.startup);
            else if (phase == CombatState.AttackRecovery) boss.Actor.Core.Tick(attack.startup + attack.active);
            Assert.That(boss.Actor.Core.State, Is.EqualTo(phase));
        }

        static CombatCore ReadyAttacker(float damage = 8, float posture = 9, float block = 20)
        {
            var attack = new AttackParameters
            {
                kind = AttackKind.Light,
                startup = .05f,
                active = .10f,
                recovery = .20f,
                damage = damage,
                posture = posture,
                block = block
            };
            var attacker = new CombatCore(new CombatTuning(), attack);
            Assert.That(attacker.RequestAttack(), Is.True);
            attacker.Tick(attack.startup);
            Assert.That(attacker.State, Is.EqualTo(CombatState.AttackActive));
            return attacker;
        }

        static HitResult HitBoss(EnemyController boss, float damage = 8, float posture = 9) =>
            ReadyAttacker(damage, posture).TryHit(boss.Actor.Core, false);

        [UnityTest]
        public IEnumerator OrdinaryPlayerHitsDamageBossWithoutCancellingStartupActiveOrRecovery()
        {
            var boss = EngageBoss();
            Assert.That(boss.Actor.Core.Tuning.ignoreOrdinaryHitStun, Is.True,
                "The MVP boss tuning must opt into ordinary-hit resistance.");
            boss.Actor.Core.AutomaticPostureRecovery = false;

            foreach (var phase in new[] { CombatState.AttackStartup, CombatState.AttackActive, CombatState.AttackRecovery })
            {
                Place(world.player, boss.Home + Vector3.back * 12f);
                BeginBossAttack(boss, phase);
                float remaining = boss.Actor.Core.Remaining;
                float health = boss.Actor.Core.Health;
                float posture = boss.Actor.Core.Posture;

                Assert.That(HitBoss(boss), Is.EqualTo(HitResult.Hit));
                Assert.That(boss.Actor.Core.Health, Is.LessThan(health), "An ordinary hit still deals health damage.");
                Assert.That(boss.Actor.Core.Posture, Is.GreaterThan(posture), "An ordinary hit still deals posture damage.");
                Assert.That(boss.Actor.Core.State, Is.EqualTo(phase), "An ordinary hit must not interrupt the current attack phase.");
                Assert.That(boss.Actor.Core.Remaining, Is.EqualTo(remaining).Within(.0001f), "The phase timer must continue from its original point.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeflectInterruptsBossAndBossCounterattackIsSlashRecordedForNextSelection()
        {
            var boss = EngageBoss();
            boss.Brain.Reset(EnemyMode.Duel);
            boss.Actor.Core.Reset();
            boss.Brain.SetRandomSeed(13);

            boss.Brain.Tick(boss.definition.wait + .01f);
            Assert.That(boss.Actor.Core.ActiveAttack.Kind, Is.EqualTo(AttackKind.Slow), "Seed 13 should choose the slow strong attack first.");
            Assert.That(boss.Actor.Core.State, Is.EqualTo(CombatState.AttackStartup));
            boss.Actor.Core.Reset(); // Retain the committed slow history while returning the boss to its guard state.
            boss.Brain.Tick(.01f);
            Assert.That(boss.Actor.Core.State, Is.EqualTo(CombatState.Guard));

            Assert.That(ReadyAttacker().TryHit(boss.Actor.Core, true), Is.EqualTo(HitResult.Block));
            Assert.That(ReadyAttacker().TryHit(boss.Actor.Core, true), Is.EqualTo(HitResult.Block));
            var deflectedPlayerAttack = ReadyAttacker();
            Assert.That(deflectedPlayerAttack.TryHit(boss.Actor.Core, true), Is.EqualTo(HitResult.Deflect));
            Assert.That(deflectedPlayerAttack.State, Is.EqualTo(CombatState.DeflectedStun));

            boss.Brain.Tick(boss.definition.counterDelay + .01f);
            Assert.That(boss.Brain.NextPattern, Is.EqualTo(AttackPattern.Slash));
            Assert.That(boss.Actor.Core.State, Is.EqualTo(CombatState.AttackStartup));
            Assert.That(boss.Actor.Core.ActiveAttack.Kind, Is.EqualTo(AttackKind.Light));

            boss.Actor.Core.Reset();
            boss.Brain.Tick(boss.definition.wait + .01f);
            Assert.That(boss.Brain.NextPattern, Is.EqualTo(AttackPattern.Slow),
                "The fast counterattack should clear the forced-fast rule, allowing the next seeded slow attack.");
            Assert.That(boss.Actor.Core.ActiveAttack.Kind, Is.EqualTo(AttackKind.Slow));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PostureBreakAndDeathStillOverrideBossHitResistance()
        {
            var boss = EngageBoss();
            boss.Actor.Core.Tuning.maxPosture = 25;
            boss.Actor.Core.AutomaticPostureRecovery = false;
            boss.Actor.Core.RestoreVitals(boss.Actor.Core.Tuning.maxHealth, 24);
            BeginBossAttack(boss, CombatState.AttackStartup);
            Assert.That(HitBoss(boss, damage: 3, posture: 5), Is.EqualTo(HitResult.Hit));
            Assert.That(boss.Actor.Core.State, Is.EqualTo(CombatState.PostureBroken),
                "A normal hit that fills posture must break the boss out of its attack.");

            boss.Actor.Core.RestoreVitals(5, 0);
            BeginBossAttack(boss, CombatState.AttackStartup);
            Assert.That(HitBoss(boss, damage: 5, posture: 0), Is.EqualTo(HitResult.Hit));
            Assert.That(boss.Actor.Core.Health, Is.Zero);
            Assert.That(boss.Actor.Core.State, Is.EqualTo(CombatState.Dead),
                "Lethal ordinary damage must take priority over boss hit resistance.");
            yield return null;
        }
    }
}
