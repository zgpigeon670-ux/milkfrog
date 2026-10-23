using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Milkfrog.CombatDemo
{
    public sealed class BullPresentation : MonoBehaviour
    {
        public Animator animator;
        public Material warningMaterial;
        public bool TelegraphVisible { get; private set; }
        public float VisualTime { get; private set; }
        BullBossEnemy boss;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] tracks;
        AnimationClip[] clips;
        LineRenderer[] lines;
        Vector3 previousPosition;
        int selected = -1;
        readonly float[] weights = new float[11];
        public void Initialize(BullBossEnemy owner)
        {
            boss = owner; VisualTime = 0; previousPosition = boss.transform.position;
            if (!graph.IsValid())
            {
                animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var d = boss.bull;
                clips = new[]{d.idle,d.walk,d.hit,d.death,d.broken,d.roar,d.Get(BullMoveKind.Horn).clip,d.Get(BullMoveKind.Kick).clip,d.Get(BullMoveKind.Charge).clip,d.Get(BullMoveKind.Slam).clip,d.Get(BullMoveKind.Wave).clip};
                graph = PlayableGraph.Create("Yellow Bull Simulation Poses"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                mixer = AnimationMixerPlayable.Create(graph,clips.Length); tracks = new AnimationClipPlayable[clips.Length];
                for(int i=0;i<tracks.Length;i++) { tracks[i] = AnimationClipPlayable.Create(graph,clips[i]); tracks[i].SetSpeed(0); graph.Connect(tracks[i],0,mixer,i); }
                AnimationPlayableOutput.Create(graph,"Bull",animator).SetSourcePlayable(mixer); graph.Play();
                lines = new LineRenderer[3];
                for(int i=0;i<lines.Length;i++)
                {
                    var go = new GameObject("Bull Attack Warning "+i); go.transform.SetParent(transform,false);
                    var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = warningMaterial; line.useWorldSpace = true;
                    line.widthMultiplier = .09f; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    line.receiveShadows = false; line.enabled = false; lines[i] = line;
                }
            }
            selected = -1; System.Array.Clear(weights,0,weights.Length); weights[0]=1; ClearTelegraph(); Advance(0);
        }
        public void BeginMove() { selected = -1; }
        public void ClearTelegraph() { TelegraphVisible = false; if(lines!=null) foreach(var line in lines) if(line!=null) line.enabled=false; }
        public void Advance(float dt)
        {
            if (!graph.IsValid()) return;
            VisualTime += dt; var core = boss.Actor.Core; int index = 0; float time = VisualTime;
            if (core.State == CombatState.Dead) { index=3; time=clips[index].length; }
            else if (core.State == CombatState.PostureBroken) { index=4; time=core.StateProgress*clips[index].length; }
            else if (core.State == CombatState.DeflectedStun || core.State == CombatState.HitStun) { index=2; time=core.StateProgress*clips[index].length; }
            else if (boss.RoarRemaining > 0) { index=5; time=(1-boss.RoarRemaining/boss.bull.roarDuration)*clips[index].length; }
            else if (boss.CurrentMove != null && core.IsAttacking)
            {
                index=6+(int)boss.CurrentMove.kind;
                float a=0,b=.45f;
                if(core.State==CombatState.AttackActive){a=.45f;b=.65f;}
                if(core.State==CombatState.AttackRecovery){a=.65f;b=1;}
                time=Mathf.Lerp(a,b,core.StateProgress)*clips[index].length;
            }
            else if ((boss.transform.position-previousPosition).sqrMagnitude>.000001f) index=1;
            if(index<2)time%=clips[index].length;
            for(int i=0;i<tracks.Length;i++)
            {
                weights[i] = Mathf.MoveTowards(weights[i],i==index?1:0,dt<=0?1:dt*14);
                mixer.SetInputWeight(i,weights[i]);
                if (i==index) tracks[i].SetTime(time);
            }
            selected=index; graph.Evaluate(0); previousPosition=boss.transform.position; DrawWarning();
        }
        void DrawWarning()
        {
            ClearTelegraph(); var move=boss.CurrentMove; var core=boss.Actor.Core;
            if(move==null || !(core.State==CombatState.AttackStartup || core.State==CombatState.AttackActive))return;
            TelegraphVisible=true; var color=boss.Danger?new Color(1,.16f,.055f):new Color(1,.73f,.15f);
            foreach(var line in lines) { line.startColor=line.endColor=color; line.widthMultiplier=move.kind==BullMoveKind.Wave && core.State==CombatState.AttackActive ? move.halfWidth*2 : .09f; }
            var origin=boss.transform.position+Vector3.up*.05f; var f=boss.LockedDirection;
            if(move.kind==BullMoveKind.Charge)
            {
                var right=Vector3.Cross(Vector3.up,f)*move.halfWidth; var end=boss.ChargeWarningEnd; end.y=origin.y;
                Poly(0,new[]{origin-right,origin+right,end+right,end-right,origin-right});
            }
            else if(move.kind==BullMoveKind.Slam)Circle(0,boss.LockedPoint,move.range);
            else if(move.kind==BullMoveKind.Wave)
            {
                if(core.State==CombatState.AttackStartup)Circle(0,boss.LockedPoint,move.range);
                else for(int i=0;i<(boss.PhaseTwo?2:1);i++)
                {
                    float elapsed=boss.ActiveTime-i*boss.bull.waveRepeatGap;
                    if(elapsed>=0 && elapsed<=move.attack.active)Circle(i,boss.LockedPoint,elapsed/move.attack.active*move.range);
                }
            }
            else
            {
                var points=new Vector3[27];points[0]=origin;
                for(int i=0;i<25;i++)points[i+1]=origin+Quaternion.AngleAxis(Mathf.Lerp(-move.halfAngle,move.halfAngle,i/24f),Vector3.up)*f*move.range;
                points[26]=origin;Poly(0,points);
            }
        }
        void Circle(int index,Vector3 center,float radius)
        {
            var points=new Vector3[65];center.y=.06f;
            for(int i=0;i<points.Length;i++){float a=i*Mathf.PI*2/64;points[i]=center+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*radius;}
            Poly(index,points);
        }
        void Poly(int index,Vector3[] points){lines[index].enabled=true;lines[index].positionCount=points.Length;lines[index].SetPositions(points);}
        void OnDestroy(){if(graph.IsValid())graph.Destroy();}
    }
}
