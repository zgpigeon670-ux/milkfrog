using UnityEngine;

namespace Milkfrog.CombatDemo
{
    public enum EnemyActivity { Patrol, Combat, Returning, Dormant, Dead }

    public abstract class EnemyController : MonoBehaviour
    {
        public string stableId;
        public EnemyDefinition definition;
        public CombatActor Actor { get; private set; }
        public EnemyActivity Activity { get; private set; }
        public Vector3 Home { get; private set; }
        public bool SuppressProximity { get; private set; }
        public abstract bool IsBoss { get; }
        public bool Alive => Actor != null && Actor.Core.State != CombatState.Dead;
        public EnemyBrain Brain { get; private set; }
        protected MvpWorld world;
        CombatDemoSettings brainSettings;
        int waypoint;
        float pause;
        bool protectedReturn;

        public void Initialize(MvpWorld owner)
        {
            world = owner; Home = transform.position;
            Actor = GetComponent<CombatActor>(); Actor.lightAttack = definition.slash;
            if (definition.slow != null) Actor.slowAttack = definition.slow;
            if (definition.perilous != null) Actor.perilousAttack = definition.perilous;
            Actor.enforceFactions = true;
            Actor.Initialize(JsonUtility.FromJson<CombatTuning>(JsonUtility.ToJson(definition.tuning)));
            Actor.Target = owner.player; Actor.AcceptsDamage = !IsBoss;
            brainSettings = ScriptableObject.CreateInstance<CombatDemoSettings>();
            brainSettings.enemySpeed = definition.speed; brainSettings.enemyWait = definition.wait;
            brainSettings.counterDelay = definition.counterDelay; brainSettings.blocksBeforeDeflect = definition.blocksBeforeDeflect;
            brainSettings.usePatternInDuel = IsBoss && definition.attackPattern != null && definition.attackPattern.Length > 0;
            brainSettings.enemyPattern = definition.attackPattern;
            Brain = new EnemyBrain(Actor, brainSettings); Brain.Reset(EnemyMode.Duel);
            Activity = IsBoss ? EnemyActivity.Dormant : EnemyActivity.Patrol;
        }

        public bool OutsideLeash(Vector3 playerPosition)
        {
            float radius = definition.patrolRadius + definition.leashMargin;
            return FlatDistance(playerPosition, Home) > radius || FlatDistance(transform.position, Home) > radius;
        }
        public void ResetForLoad()
        {
            gameObject.SetActive(true); Actor.Motor.enabled = false;
            transform.SetPositionAndRotation(Home, Quaternion.identity); Actor.Motor.enabled = true;
            Actor.Core.Reset(); Actor.Target = world.player; Actor.AcceptsDamage = !IsBoss;
            Activity = IsBoss ? EnemyActivity.Dormant : EnemyActivity.Patrol;
            SuppressProximity = protectedReturn = false; waypoint = 0; pause = 0; Brain.Reset(EnemyMode.Duel);
        }
        public static float FlatDistance(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;
        public void UpdateRearm()
        {
            if (FlatDistance(world.player.transform.position, transform.position) > definition.alertRadius)
                SuppressProximity = false;
        }
        public void Engage()
        {
            if (!Alive) return;
            Activity = EnemyActivity.Combat; Actor.AcceptsDamage = true; protectedReturn = false;
            Brain.Reset(EnemyMode.Duel);
        }
        public void ReturnHome(bool protect)
        {
            if (!Alive || IsBoss) return;
            Activity = EnemyActivity.Returning; SuppressProximity = true; protectedReturn = protect;
            Actor.AcceptsDamage = !protect;
            Actor.Core.RestoreVitals(Actor.Core.Health, Actor.Core.Posture);
            Brain.Reset(EnemyMode.Duel);
        }
        public void MarkDead()
        {
            Activity = EnemyActivity.Dead; Actor.AcceptsDamage = false;
            Actor.Motor.enabled = false;
        }
        public void Tick(float dt)
        {
            if (!Alive) { MarkDead(); return; }
            if (Activity == EnemyActivity.Combat) { Brain.Tick(dt); return; }
            if (Activity == EnemyActivity.Dormant) return;
            Actor.Core.SetGuard(false, false);
            if (Activity == EnemyActivity.Returning)
            {
                MoveToward(Home, dt);
                if (FlatDistance(transform.position, Home) < .12f)
                {
                    Actor.Core.Reset(); Activity = EnemyActivity.Patrol; pause = definition.patrolPause;
                    protectedReturn = false; Actor.AcceptsDamage = world.Director.State != EncounterState.BossCombat;
                }
                else if (protectedReturn) Actor.AcceptsDamage = false;
                return;
            }
            if (pause > 0) { pause -= dt; return; }
            float angle = waypoint * Mathf.PI * 2 / 3;
            Vector3 point = Home + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * definition.patrolRadius;
            MoveToward(point, dt);
            if (FlatDistance(transform.position, point) < .12f)
            { waypoint = (waypoint + 1) % 3; pause = definition.patrolPause; }
        }
        void MoveToward(Vector3 point, float dt)
        {
            Vector3 delta = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (delta.sqrMagnitude < .001f || !Actor.Core.CanAct) return;
            transform.rotation = Quaternion.LookRotation(delta);
            Actor.Move(delta.normalized, Mathf.Min(definition.speed, delta.magnitude / Mathf.Max(dt, .0001f)), dt);
        }
        void OnDestroy() { if (brainSettings != null) Destroy(brainSettings); }
    }
}
