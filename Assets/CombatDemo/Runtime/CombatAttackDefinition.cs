using System;
using UnityEngine;
namespace Milkfrog.CombatDemo
{
    public enum AttackKind { Light, Thrust, Followup, Slow, Perilous }
    public enum AttackResponse { Deflectable, DodgeOnly }
    [Serializable]
    public sealed class AttackParameters
    {
        public AttackKind kind;
        public AttackResponse response;
        public float startup=.25f, active=.10f, recovery=.35f;
        public float damage=10, maxDamage=10, posture=10, maxPosture=10, block=20, maxBlock=20;
        public float deflectPosture=30;
        public float cancelWindow=.16f;
        public bool CanComboFrom;
        public float comboWindow=.18f;
        public AttackSnapshot Snapshot(float charge) => new AttackSnapshot(kind,response,startup,active,recovery,
            Mathf.Lerp(damage,maxDamage,charge),Mathf.Lerp(posture,maxPosture,charge),Mathf.Lerp(block,maxBlock,charge),deflectPosture,cancelWindow);
        public static AttackParameters Thrust() => new AttackParameters {kind=AttackKind.Thrust,startup=.12f,active=.12f,recovery=.42f,maxDamage=22,maxPosture=28,maxBlock=35};
    }
    public readonly struct AttackSnapshot
    {
        public readonly AttackKind Kind;
        public readonly AttackResponse Response;
        public readonly float Startup,Active,Recovery,Damage,Posture,Block,DeflectPosture,CancelWindow;
        public AttackSnapshot(AttackKind kind,AttackResponse response,float startup,float active,float recovery,float damage,float posture,float block,float deflectPosture,float cancelWindow)
        {Kind=kind;Response=response;Startup=startup;Active=active;Recovery=recovery;Damage=damage;Posture=posture;Block=block;DeflectPosture=deflectPosture;CancelWindow=cancelWindow;}
        public static AttackSnapshot Light(CombatTuning t) => new AttackSnapshot(AttackKind.Light,AttackResponse.Deflectable,t.startup,t.active,t.recovery,t.hitDamage,t.hitPosture,t.blockPosture,t.deflectPosture,.16f);
    }
    [CreateAssetMenu(menuName="Combat Demo/Attack Definition")]
    public sealed class CombatAttackDefinition : ScriptableObject
    {
        public AttackParameters rules=new AttackParameters();
        public AnimationClip clip;
        [Range(0,1)] public float activeStart=.25f,activeEnd=.38f;
        public CombatBladeTrace trace;
        public float SampleTime(CombatCore core)
        {
            float a=0,b=activeStart;
            if(core.State==CombatState.AttackActive){a=activeStart;b=activeEnd;}
            if(core.State==CombatState.AttackRecovery){a=activeEnd;b=1;}
            return Mathf.Lerp(a,b,core.StateProgress)*clip.length;
        }
    }
}
