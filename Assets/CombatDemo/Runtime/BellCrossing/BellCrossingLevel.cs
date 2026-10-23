using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Milkfrog.CombatDemo.BellCrossing
{
    // Standalone session: no MvpWorld, GameFlowController or SaveService is created.
    public sealed class BellCrossingLevel : MonoBehaviour
    {
        public CombatDemoSettings combat;
        public EnemyDefinition mobDefinition, bossDefinition;
        public GameObject playerPrefab, enemyPrefab, bossPrefab, architecture;
        public DemoCamera cameraRig;
        public Camera overviewCamera;
        public CombatFeedback feedback;
        public bool manualSimulation;
        public CombatActor Player { get; private set; }
        public CombatActor[] Enemies { get; private set; }
        public bool WestSeal { get; private set; }
        public bool EastSeal { get; private set; }
        public bool ShortcutOpen { get; private set; }
        public bool CheckpointLit { get; private set; }
        public bool BossActive { get; private set; }
        public bool BossDefeated => Enemies != null && Enemies[BellCrossingLayout.Boss].Core.State == CombatState.Dead;
        public bool Completed { get; private set; }
        public bool Paused { get; private set; }
        public bool MapOpen { get; private set; }
        public bool OfferingTaken { get; private set; }
        public int Medicines { get; private set; } = 3;
        public int Deaths { get; private set; }
        public float Elapsed { get; private set; }
        public string Notice { get; private set; } = "渡钟已哑。找回西侧灰烬印与东侧潮声印，再去北殿。";
        public bool[] Visited { get; } = new bool[BellCrossingLayout.Cells.Length];
        public CrossingRoom? CurrentRoom => Player != null ? BellCrossingLayout.At(Player.transform.position) : null;
        public string Objective => Completed ? "渡钟重鸣 · 本关完成" : BossDefeated ? "前往北侧渡钟台，按 E 敲响渡钟" :
            WestSeal && EastSeal ? "双印齐备 · 穿过北门，击败守钟者" : "找回两枚钟印  " + (WestSeal ? "灰烬 ✓" : "灰烬 ○") + "  /  " + (EastSeal ? "潮声 ✓" : "潮声 ○");
        readonly List<CombatDemoSettings> brainSettings = new List<CombatDemoSettings>();
        readonly List<CombatAnimationPresenter> animations = new List<CombatAnimationPresenter>();
        EnemyBrain[] brains;
        bool[] engaged;
        GameObject shortcutGate, sealGate, arenaEntrance, arenaExit, westToken, eastToken, offering;
        DemoInput input;
        CombatActor locked;
        Vector2 movement;
        bool attackNeedsRelease = true, guardNeedsRelease = true, ignoreLook = true;
        float deathClock, noticeClock = 9;
        Font font;
        GUIStyle textStyle, titleStyle, smallStyle, buttonStyle;
        AudioClip bellSound;

        void Awake()
        {
            font = Resources.Load<Font>("NotoSansSC-VF");
            if (architecture == null) throw new InvalidOperationException("Generate the BellCrossing scene with BellCrossingAuthoring.Create first.");
            shortcutGate = Find("Shortcut gate"); sealGate = Find("Two seal gate"); arenaEntrance = Find("Arena entrance"); arenaExit = Find("Arena exit");
            westToken = Find("West bell seal"); eastToken = Find("East bell seal"); offering = Find("Pilgrim offering");
            input = new DemoInput("<Mouse>/middleButton");
            Player = Instantiate(playerPrefab, BellCrossingLayout.Center(CrossingRoom.Dock), Quaternion.identity, transform).GetComponent<CombatActor>();
            Player.isPlayer = true; Player.enforceFactions = true; Player.autoFaceGuardAttacks = true; Player.Initialize(Clone(combat.player));
            Enemies = new CombatActor[BellCrossingLayout.EnemyRooms.Length]; brains = new EnemyBrain[Enemies.Length]; engaged = new bool[Enemies.Length];
            for (int i = 0; i < Enemies.Length; i++)
            {
                bool boss = i == BellCrossingLayout.Boss; var definition = boss ? bossDefinition : mobDefinition;
                var instance = Instantiate(boss ? bossPrefab : enemyPrefab, BellCrossingLayout.Home(i), Quaternion.Euler(0,180,0), transform);
                foreach (var controller in instance.GetComponents<EnemyController>()) controller.enabled = false;
                var actor = Enemies[i] = instance.GetComponent<CombatActor>(); actor.isPlayer = false; actor.enforceFactions = true;
                actor.lightAttack = definition.slash; actor.slowAttack = definition.slow; actor.perilousAttack = definition.perilous;
                actor.Initialize(Clone(definition.tuning)); actor.Target = Player; actor.AcceptsDamage = !boss;
                var config = ScriptableObject.CreateInstance<CombatDemoSettings>(); brainSettings.Add(config);
                config.enemySpeed = definition.speed; config.enemyWait = definition.wait; config.counterDelay = definition.counterDelay;
                config.blocksBeforeDeflect = definition.blocksBeforeDeflect; config.usePatternInDuel = boss;
                config.enemyPattern = definition.attackPattern; config.randomAttacks = boss && definition.randomAttacks;
                config.slashWeight = definition.slashWeight; config.slowWeight = definition.slowWeight; config.perilousWeight = definition.perilousWeight;
                brains[i] = new EnemyBrain(actor,config,1910+i); brains[i].Reset(EnemyMode.Duel);
            }
            foreach (var animation in GetComponentsInChildren<CombatAnimationPresenter>()) { animation.Initialize(); animations.Add(animation); }
            foreach (var view in GetComponentsInChildren<CombatActorView>()) { view.viewCamera = cameraRig.GetComponent<Camera>(); view.diagnosticsOnly = true; }
            foreach (var label in GetComponentsInChildren<TextMesh>()) label.gameObject.SetActive(false);
            var actors = new List<CombatActor> { Player }; actors.AddRange(Enemies); feedback.actors = actors.ToArray();
            feedback.ResolveActor = core => { foreach (var actor in feedback.actors) if (actor.Core == core) return actor; return null; };
            Player.Core.HitResolved += OnHit;
            cameraRig.player = Player.transform; cameraRig.freeOrbit = true; cameraRig.avoidObstacles = true; cameraRig.orbitDistance = 3.8f;
            Restart();
        }
        GameObject Find(string name)
        {
            foreach (var child in architecture.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child.gameObject;
            throw new InvalidOperationException("Missing level fixture: " + name);
        }
        static CombatTuning Clone(CombatTuning tuning) => JsonUtility.FromJson<CombatTuning>(JsonUtility.ToJson(tuning));
        void OnHit(HitEvent hit)
        {
            var actor = feedback.ResolveActor(hit.Attacker);
            feedback.OnContact(new CombatContact(hit,actor != null && actor.HasContactPoint ? actor.LastContactPoint : Player.transform.position + Vector3.up));
        }
        void Notify(string text) { Notice = text; noticeClock = 7; }
        public void Restart()
        {
            WestSeal = EastSeal = ShortcutOpen = CheckpointLit = OfferingTaken = Completed = BossActive = false;
            Medicines = 3; Deaths = 0; Elapsed = 0; Array.Clear(Visited,0,Visited.Length);
            foreach (var enemy in Enemies) enemy.ResetActor();
            Respawn(); SetPaused(false); SetMap(false);
            Notify("渡钟已哑。循铜灯取灰烬印，循青灯取潮声印。留火院可休息。" );
        }
        public void Respawn()
        {
            BossActive = false; locked = null; deathClock = 0; Medicines = Mathf.Max(3,Medicines);
            Player.ResetActor(); Place(Player, BellCrossingLayout.Center(CheckpointLit ? CrossingRoom.Hearth : CrossingRoom.Dock));
            for (int i = 0; i < Enemies.Length; i++)
            {
                var actor = Enemies[i]; engaged[i] = false;
                if (actor.Core.State != CombatState.Dead) { actor.ResetActor(); brains[i].Reset(EnemyMode.Duel); }
                else actor.Motor.enabled = false;
                actor.AcceptsDamage = i != BellCrossingLayout.Boss;
            }
            foreach (var animation in animations) animation.ResetVisuals();
            ClearInput(); UpdateGates(); Physics.SyncTransforms();
            cameraRig.yaw = 0; cameraRig.pitch = 16; cameraRig.ResetImpulse(); cameraRig.Step(1); UpdateCursor();
        }
        public static void Place(CombatActor actor,Vector3 position) { actor.Motor.enabled = false; actor.transform.position = position; actor.Motor.enabled = true; Physics.SyncTransforms(); }
        void Update()
        {
            if (manualSimulation) return;
            var keyboard = Keyboard.current;
            if (input.PausePressed) { if (MapOpen) SetMap(false); else SetPaused(!Paused); }
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame && !Completed) SetMap(!MapOpen);
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame && (Paused || Completed)) { Restart(); return; }
            if (Paused || MapOpen || Completed) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime,.1f);
            if (Player.Core.State == CombatState.Dead || Player.transform.position.y < -3)
            {
                deathClock += dt; foreach (var animation in animations) animation.AdvanceVisual(dt);
                if (deathClock >= 1.7f) { Deaths++; Respawn(); Notify("在留火处醒来。已取得的钟印、捷径和击败记录保留。" ); }
                return;
            }
            if (ignoreLook) ignoreLook = false; else cameraRig.Look(input.Look);
            SubmitInput(input.Snapshot); Simulate(dt);
            if (input.InteractPressed) Interact();
        }
        Vector3 Direction(Vector2 value)
        {
            var forward = Vector3.ProjectOnPlane(cameraRig.transform.forward,Vector3.up).normalized;
            return forward * value.y + Vector3.Cross(Vector3.up,forward) * value.x;
        }
        CombatActor Closest(float radius)
        {
            CombatActor best = null; float distance = radius * radius;
            foreach (var enemy in Enemies)
            {
                if (enemy.Core.State == CombatState.Dead || !enemy.AcceptsDamage) continue;
                float d = (enemy.transform.position - Player.transform.position).sqrMagnitude;
                if (d < distance && Player.HasLineOfSight(enemy,enemy.transform.position+Vector3.up)) { distance = d; best = enemy; }
            }
            return best;
        }
        public void SubmitInput(CombatInputFrame frame)
        {
            if (Paused || MapOpen || Completed || Player.Core.State == CombatState.Dead) return;
            movement = Vector2.ClampMagnitude(frame.move,1);
            if (!frame.attackHeld) attackNeedsRelease = false;
            if (!frame.guardHeld) guardNeedsRelease = false;
            if (frame.lockPressed) locked = locked != null ? null : Closest(12);
            if (locked != null && (locked.Core.State == CombatState.Dead || (locked.transform.position-Player.transform.position).sqrMagnitude > 144 || !Player.HasLineOfSight(locked,locked.transform.position+Vector3.up))) locked = null;
            Player.Target = locked != null ? locked : Closest(Player.Core.Tuning.range+1);
            cameraRig.lockOn = locked != null; cameraRig.enemy = locked != null ? locked.transform : null;
            if (feedback.Clock.Remaining > 0) return;
            if (frame.itemPressed) UseMedicine();
            Player.Core.SetAttackHeld(frame.attackHeld);
            if (frame.dodgePressed) { Player.RequestDodge(Direction(movement)); attackNeedsRelease = frame.attackHeld; }
            if (frame.jumpPressed) Player.TryJump();
            bool guard = frame.guardHeld && !guardNeedsRelease;
            Player.Core.SetGuard(guard,frame.guardPressed && !guardNeedsRelease);
            if (guard) { attackNeedsRelease = frame.attackHeld; return; }
            if (Player.Core.IsPreparing && (!frame.attackHeld || frame.attackReleased)) Player.Core.ReleaseAttack();
            else if (frame.attackPressed && !attackNeedsRelease) { if (Player.Target != null) Player.FaceTarget(); Player.BeginPlayerAttack(); attackNeedsRelease = frame.attackHeld; }
        }
        public bool UseMedicine()
        {
            if (Paused || MapOpen || Completed || Medicines <= 0 || !Player.Core.TryRecoverWithItem(40,15)) return false;
            Medicines--; Notify("服下回生药 · 生命 +40，架势 -15" ); return true;
        }
        public void Simulate(float dt)
        {
            if (dt < 0 || float.IsNaN(dt) || float.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            if (Paused || MapOpen || Completed || Player.Core.State == CombatState.Dead) return;
            while (dt > 0)
            {
                float step = Mathf.Min(dt,1f/120); dt -= step; Elapsed += step; noticeClock = Mathf.Max(0,noticeClock-step);
                feedback.TickReal(step); step = feedback.Clock.Consume(step); if (step <= 0) continue;
                var direction = Direction(movement);
                if (direction.sqrMagnitude > .001f && Player.Core.CanAct && !Player.Core.IsPreparing) Player.transform.rotation = Quaternion.LookRotation(direction);
                Player.Move(direction,combat.playerSpeed,step);
                var room = CurrentRoom; if (room.HasValue) Visited[(int)room.Value] = true;
                if (!BossDefeated && WestSeal && EastSeal && room == CrossingRoom.Guardian && Player.transform.position.z > 24 && !BossActive)
                { BossActive = true; Enemies[BellCrossingLayout.Boss].AcceptsDamage = true; UpdateGates(); Notify("守钟者苏醒。赤色危招用垫步躲避，弹开普通攻击可积累架势。" ); }
                for (int i = 0; i < Enemies.Length; i++)
                {
                    var enemy = Enemies[i]; if (enemy.Core.State == CombatState.Dead) { enemy.Motor.enabled = false; continue; }
                    bool active = i == BellCrossingLayout.Boss ? BossActive : room == BellCrossingLayout.EnemyRooms[i] && enemy.HasLineOfSight(Player,Player.transform.position+Vector3.up);
                    if (active) { engaged[i] = true; brains[i].Tick(step); }
                    else
                    {
                        if (engaged[i]) { enemy.Core.RestoreVitals(enemy.Core.Health,enemy.Core.Posture); brains[i].Reset(EnemyMode.Duel); engaged[i] = false; }
                        enemy.Core.SetGuard(false,false);
                        var delta = BellCrossingLayout.Home(i)-enemy.transform.position; delta.y = 0;
                        if (delta.sqrMagnitude > .01f && enemy.Core.CanAct) enemy.transform.rotation = Quaternion.LookRotation(delta);
                        enemy.Move(delta.normalized,Mathf.Min(brainSettings[i].enemySpeed,delta.magnitude/step),step);
                    }
                    var center = BellCrossingLayout.Center(BellCrossingLayout.EnemyRooms[i]); var p = enemy.transform.position;
                    var confined = new Vector3(Mathf.Clamp(p.x,center.x-4.8f,center.x+4.8f),p.y,Mathf.Clamp(p.z,center.z-4.8f,center.z+4.8f));
                    if ((p-confined).sqrMagnitude > .000001f) enemy.Motor.Move(confined-p);
                }
                Physics.SyncTransforms(); Player.Core.Tick(step);
                foreach (var enemy in Enemies) if (enemy.Core.State != CombatState.Dead) enemy.Core.Tick(step);
                foreach (var animation in animations) animation.AdvanceVisual(step);
                if (BossActive && BossDefeated) { BossActive = false; UpdateGates(); Notify("守钟者已倒下。沿金色地纹走向渡钟台。" ); }
                if (Player.Core.State == CombatState.Dead) break;
            }
        }
        bool Near(Vector3 point) => (Player.transform.position-point).sqrMagnitude <= 2.6f*2.6f;
        bool Cleared(CrossingRoom room)
        {
            for (int i = 0; i < Enemies.Length; i++) if (BellCrossingLayout.EnemyRooms[i] == room && Enemies[i].Core.State != CombatState.Dead) return false;
            return true;
        }
        bool Safe => !BossActive && (!CurrentRoom.HasValue || Cleared(CurrentRoom.Value));
        public string Prompt
        {
            get
            {
                if (Completed || Player == null || Player.Core.State == CombatState.Dead) return null;
                if (Near(BellCrossingLayout.RestPoint)) return "E  留火休息 · 激活复活点，补满生命与至少 3 份药";
                if (!WestSeal && Near(BellCrossingLayout.WestPoint)) return Cleared(CrossingRoom.WestSeal) ? "E  取回灰烬印" : "先击败灰烬钟龛的守卫";
                if (!EastSeal && Near(BellCrossingLayout.EastPoint)) return Cleared(CrossingRoom.EastSeal) ? "E  取回潮声印" : "先击败潮声钟龛的两名守卫";
                if (!ShortcutOpen && CurrentRoom == CrossingRoom.WestReturn && Near(BellCrossingLayout.LeverPoint)) return "E  转动绞盘 · 打开通向门庭的捷径";
                if (!OfferingTaken && Near(BellCrossingLayout.ShrinePoint)) return Cleared(CrossingRoom.Shrine) ? "E  取走旅人的供药 · 额外 2 份" : "小祠仍有守卫";
                if (Near(BellCrossingLayout.BellPoint)) return BossDefeated ? "E  敲响渡钟" : "渡钟正被守钟者的力量封住";
                if (!WestSeal || !EastSeal) if (CurrentRoom == CrossingRoom.Junction) return "北门需要灰烬印与潮声印 · 西铜东青";
                return null;
            }
        }
        public bool Interact()
        {
            if (Paused || MapOpen || Completed || !Player.Core.CanAct || feedback.Clock.Remaining > 0 || !Safe) return false;
            bool changed = true;
            if (Near(BellCrossingLayout.RestPoint)) { CheckpointLit = true; Player.Core.RestoreVitals(Player.Core.Tuning.maxHealth,0); Medicines = Mathf.Max(3,Medicines); Notify("留火已点燃。此后在这里复活；本次探索进度会保留。" ); }
            else if (!WestSeal && Near(BellCrossingLayout.WestPoint)) { WestSeal = true; westToken.SetActive(false); Notify("取得灰烬印。前方回望廊的绞盘可开启捷径。" ); }
            else if (!EastSeal && Near(BellCrossingLayout.EastPoint)) { EastSeal = true; eastToken.SetActive(false); Notify("取得潮声印。东侧小祠有供药，归潮廊可返回门庭。" ); }
            else if (!ShortcutOpen && CurrentRoom == CrossingRoom.WestReturn && Near(BellCrossingLayout.LeverPoint)) { ShortcutOpen = true; Notify("铜栅升起。回望廊与双印门庭已连通。" ); }
            else if (!OfferingTaken && Near(BellCrossingLayout.ShrinePoint)) { OfferingTaken = true; Medicines += 2; offering.SetActive(false); Notify("供药 +2。碑文：‘钟声会把仍记得的人带回家。’" ); }
            else if (BossDefeated && Near(BellCrossingLayout.BellPoint)) { Completed = true; ClearInput(); Ring(); UpdateCursor(); }
            else changed = false;
            UpdateGates(); return changed;
        }
        void UpdateGates()
        {
            shortcutGate.SetActive(!ShortcutOpen); sealGate.SetActive(!(WestSeal && EastSeal));
            arenaEntrance.SetActive(BossActive && !BossDefeated); arenaExit.SetActive(!BossDefeated);
            westToken.SetActive(!WestSeal); eastToken.SetActive(!EastSeal); offering.SetActive(!OfferingTaken);
        }
        void Ring()
        {
            const int rate = 22050; var samples = new float[rate*5];
            for (int i=0;i<samples.Length;i++) { float t=(float)i/rate; samples[i]=.22f*Mathf.Exp(-t*.9f)*(Mathf.Sin(t*2*Mathf.PI*196)+.45f*Mathf.Sin(t*2*Mathf.PI*523)+.2f*Mathf.Sin(t*2*Mathf.PI*811)); }
            bellSound=AudioClip.Create("Crossing bell",samples.Length,1,rate,false);bellSound.SetData(samples,0);
            var source=gameObject.GetComponent<AudioSource>()??gameObject.AddComponent<AudioSource>();source.PlayOneShot(bellSound,.65f);
        }
        public void ClearInput()
        {
            movement=Vector2.zero;attackNeedsRelease=guardNeedsRelease=ignoreLook=true;locked=null;
            if (Player==null) return;
            Player.Target=null;Player.Core.SetAttackHeld(false);Player.Core.SetGuard(false,false);Player.Core.CancelPreparation();
            feedback.ResetFeedback();cameraRig.ReleaseLock();
        }
        public void SetPaused(bool value) { Paused=value;ClearInput();UpdateCursor(); }
        public void SetMap(bool value)
        {
            MapOpen=value;ClearInput();cameraRig.GetComponent<Camera>().enabled=!value;overviewCamera.gameObject.SetActive(value);UpdateCursor();
        }
        void UpdateCursor() { bool free=Paused||MapOpen||Completed;cameraRig.Suspended=free;Cursor.lockState=free?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=free; }
        void OnApplicationFocus(bool focus) { if(!focus && Player!=null && !manualSimulation) SetPaused(true); }
        void OnDestroy() { input?.Dispose();foreach(var setting in brainSettings) Destroy(setting);if(bellSound!=null) Destroy(bellSound);Cursor.lockState=CursorLockMode.None;Cursor.visible=true; }

        static void Fill(Rect rect,Color color) { var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old; }
        void Styles()
        {
            if(textStyle!=null)return;
            textStyle=new GUIStyle(GUI.skin.label){font=font,fontSize=17,wordWrap=true,normal={textColor=new Color(.87f,.9f,.85f)}};
            titleStyle=new GUIStyle(textStyle){fontSize=30,fontStyle=FontStyle.Bold};smallStyle=new GUIStyle(textStyle){fontSize=14};
            buttonStyle=new GUIStyle(GUI.skin.button){font=font,fontSize=18,fixedHeight=42};
        }
        void Bar(Rect rect,float value,float maximum,Color color) { Fill(rect,new Color(.07f,.09f,.1f,.95f));Fill(new Rect(rect.x,rect.y,rect.width*Mathf.Clamp01(value/maximum),rect.height),color); }
        void OnGUI()
        {
            if(Player==null)return;Styles();float scale=Mathf.Max(.4f,Screen.height/900f);GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));float w=Screen.width/scale,h=Screen.height/scale;
            Fill(new Rect(22,22,410,164),new Color(.025f,.045f,.05f,.92f));GUI.Label(new Rect(40,30,370,44),"沉钟渡 / SILENT BELL",titleStyle);
            GUI.Label(new Rect(40,75,370,27),(CurrentRoom.HasValue?BellCrossingLayout.Names[(int)CurrentRoom.Value]:"渡口之外")+"  ·  回生药 "+Medicines+" [Q]",textStyle);
            Bar(new Rect(40,111,350,13),Player.Core.Health,Player.Core.Tuning.maxHealth,new Color(.55f,.8f,.45f));
            Bar(new Rect(40,133,350,8),Player.Core.Posture,Player.Core.Tuning.maxPosture,new Color(.84f,.65f,.3f));
            GUI.Label(new Rect(40,148,360,25),$"生命 {Player.Core.Health:0} / {Player.Core.Tuning.maxHealth:0}   架势 {Player.Core.Posture:0}",smallStyle);
            Fill(new Rect(w-480,22,458,74),new Color(.025f,.045f,.05f,.92f));GUI.Label(new Rect(w-462,33,420,58),Objective,textStyle);
            if(noticeClock>0&&!MapOpen&&!Completed) { Fill(new Rect(w*.5f-270,202,540,70),new Color(.025f,.045f,.05f,.85f));GUI.Label(new Rect(w*.5f-253,211,506,58),Notice,textStyle); }
            string prompt=Prompt;if(!Paused&&!MapOpen&&!Completed&&prompt!=null) { Fill(new Rect(w*.5f-325,h-150,650,54),new Color(.025f,.045f,.05f,.92f));GUI.Label(new Rect(w*.5f-310,h-140,620,40),prompt,textStyle); }
            if(BossActive)
            {
                var core=Enemies[BellCrossingLayout.Boss].Core;GUI.Label(new Rect(w*.5f-200,h-220,400,30),"守钟者 · THE KEEPER",textStyle);
                Bar(new Rect(w*.5f-240,h-187,480,12),core.Health,core.Tuning.maxHealth,new Color(.74f,.3f,.2f));
                Bar(new Rect(w*.5f-240,h-166,480,7),core.Posture,core.Tuning.maxPosture,new Color(.92f,.68f,.2f));
                if(core.State==CombatState.AttackStartup&&core.ActiveAttack.Kind==AttackKind.Perilous)
                { Fill(new Rect(w*.5f-90,h*.44f-4,180,52),new Color(.63f,.045f,.035f,.95f));GUI.Label(new Rect(w*.5f-65,h*.44f,160,48),"危 · 垫步",titleStyle); }
            }
            else if(locked!=null)GUI.Label(new Rect(w*.5f-170,h-200,340,34),$"锁定 · HP {locked.Core.Health:0}  架势 {locked.Core.Posture:0}",textStyle);
            Fill(new Rect(22,h-74,w-44,52),new Color(.025f,.045f,.05f,.92f));GUI.Label(new Rect(38,h-65,w-76,44),"WASD 移动 · 鼠标镜头 · 空格跳跃 · Shift 垫步 · 左键攻击/按住蓄力 · 右键防御/弹开 · 中键锁定 · E 交互 · Q 回生药 · M 地图 · Esc 暂停",smallStyle);
            if(Player.Core.State==CombatState.Dead)GUI.Label(new Rect(w*.5f-180,h*.43f,420,100),"钟声未至\n正在返回留火处……",titleStyle);
            if(MapOpen)DrawMap(w,h);
            else if(Paused||Completed)DrawPanel(w,h);
        }
        void DrawPanel(float w,float h)
        {
            Fill(new Rect(w*.5f-260,h*.5f-175,520,350),new Color(.025f,.045f,.05f,.97f));
            GUILayout.BeginArea(new Rect(w*.5f-235,h*.5f-155,470,320));
            GUILayout.Label(Completed?"渡钟重鸣":"留步片刻",titleStyle);
            GUILayout.Label(Completed?"雾后有一声回响。离去的人，终于记起了归途。":Objective,textStyle);
            GUILayout.Label($"探索 {Elapsed/60:00}:{Elapsed%60:00}  ·  倒下 {Deaths} 次  ·  供药 {(OfferingTaken?"已取得":"未取得")}",textStyle);GUILayout.Space(15);
            if(!Completed&&GUILayout.Button("继续探索",buttonStyle))SetPaused(false);
            if(!Completed&&GUILayout.Button("返回留火处（保留本次进度）",buttonStyle)){Respawn();SetPaused(false);}
            if(GUILayout.Button("R  重新开始本关",buttonStyle))Restart();
            if(GUILayout.Button("退出试玩",buttonStyle))Application.Quit();
            GUILayout.EndArea();
        }
        void DrawMap(float w,float h)
        {
            Fill(new Rect(w*.5f-370,110,740,h-210),new Color(.025f,.045f,.05f,.97f));
            GUI.Label(new Rect(w*.5f-345,125,690,45),"沉钟渡 · 西铜东青，双印开北门",titleStyle);
            float unit=Mathf.Min(67,(h-325)/7),left=w*.5f-40,top=200;
            Func<int,Vector2> point=i=>new Vector2(left+BellCrossingLayout.Cells[i].x*unit,top+(3-BellCrossingLayout.Cells[i].y)*unit);
            for(int i=0;i<BellCrossingLayout.Edges.GetLength(0);i++)
            {
                int a=BellCrossingLayout.Edges[i,0],b=BellCrossingLayout.Edges[i,1];var p=point(a);var q=point(b);
                Color color=new Color(.3f,.4f,.4f);if(a==6&&b==7&&!ShortcutOpen||a==7&&b==13&&!(WestSeal&&EastSeal))color=new Color(.8f,.25f,.15f);
                Fill(new Rect(Mathf.Min(p.x,q.x)-2,Mathf.Min(p.y,q.y)-2,Mathf.Abs(p.x-q.x)+4,Mathf.Abs(p.y-q.y)+4),color);
            }
            for(int i=0;i<BellCrossingLayout.Cells.Length;i++)
            {
                var p=point(i);Fill(new Rect(p.x-25,p.y-17,50,34),Visited[i]?new Color(.32f,.42f,.4f):new Color(.12f,.18f,.2f));
                GUI.Label(new Rect(p.x-30,p.y-14,62,32),BellCrossingLayout.Names[i],new GUIStyle(smallStyle){fontSize=11,alignment=TextAnchor.MiddleCenter});
                if(CurrentRoom==(CrossingRoom)i)Fill(new Rect(p.x-3,p.y-3,6,6),Color.yellow);
            }
            GUI.Label(new Rect(w*.5f-343,h-159,680,48),"黄点：你的位置 · 红线：尚未开启的门 · 留火院：休息与复活\n灰烬印在西北钟龛；潮声印在东北钟龛；小祠为可选支路。",smallStyle);
            if(GUI.Button(new Rect(w*.5f+210,125,125,40),"M / 关闭",buttonStyle)){SetMap(false);SetPaused(false);}
        }
    }
}
