using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Milkfrog.CombatDemo
{
    public enum GameFlowState { MainMenu, Loading, Playing, PlayerDead, Victory }
    public sealed class GameFlowController : MonoBehaviour
    {
        public bool mainMenu;
        public static string SaveDirectoryOverride;
        static PlayerSnapshot pending;
        public GameFlowState State { get; private set; }
        public bool Paused { get; private set; }
        public string Message { get; private set; } = "";
        public SaveService Store { get; private set; }
        MvpWorld world;
        MvpMenuView menu;
        public BonfireCheckpoint ActiveBonfire { get; private set; }
        bool attributesFromBonfire;
        float messageTime;

        void Start()
        {
            if (Debug.isDebugBuild && SaveDirectoryOverride == null)
            {
                var args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i + 1 < args.Length; i++)
                    if (args[i] == "--mvp-save-dir") { SaveDirectoryOverride = args[i + 1]; break; }
            }
            Store = new SaveService(SaveDirectoryOverride); menu = GetComponent<MvpMenuView>(); world = GetComponent<MvpWorld>();
            if (mainMenu) { State = GameFlowState.MainMenu; ShowHome(); }
            else
            {
                State = GameFlowState.Playing;
                PlayerSnapshot snapshot = pending; pending = null;
                if (snapshot == null) Store.TryLoad(out snapshot);
                world.Restore(snapshot); SetCursor(false); menu.Hide();
                if (snapshot == null) Save();
            }
        }
        void Update() { if (messageTime > 0 && (messageTime -= Time.unscaledDeltaTime) <= 0) Message = ""; }
        public void Notify(string message) { Message = message; messageTime = 6; }
        void SetCursor(bool visible)
        { Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = visible; }
        void ShowHome()
        {
            SetCursor(true);
            menu.Show("MILKFROG", "A swordsman's trial\nExplore. Deflect. Survive.",
                new MenuAction("Start Game / Continue", Continue), new MenuAction("New Game", NewGame), new MenuAction("Quit", Quit));
        }
        void Continue()
        {
            if (Store.TryLoad(out var snapshot)) LoadLevel(snapshot);
            else if (!Store.HasSave) CreateNew();
            else menu.Show("SAVE UNAVAILABLE", Store.LastMessage + "\nThe original files have been preserved.",
                new MenuAction("Start New Game", NewGame), new MenuAction("Back", ShowHome));
        }
        void NewGame()
        {
            if (Store.HasSave)
                menu.Show("NEW GAME", "Replace your saved progress?", new MenuAction("Confirm New Game", CreateNew), new MenuAction("Back", ShowHome));
            else CreateNew();
        }
        void CreateNew()
        {
            var snapshot = new PlayerSnapshot { position = new Vector3(0, .02f, -35) };
            if (!Store.TrySave(snapshot)) { menu.Show("SAVE FAILED", Store.LastMessage, new MenuAction("Back", ShowHome)); return; }
            LoadLevel(snapshot);
        }
        public void LoadLevel(PlayerSnapshot snapshot) { pending = snapshot; BeginLoad("MVP_TestLevel"); }
        void BeginLoad(string scene)
        {
            if (State == GameFlowState.Loading) return;
            State = GameFlowState.Loading; Paused = false; world?.ClearInput(); SetCursor(true);
            menu.Show("LOADING", "Preparing the training grounds..."); StartCoroutine(Load(scene));
        }
        IEnumerator Load(string scene)
        {
            yield return null;
            yield return SceneManager.LoadSceneAsync(scene);
        }
        public bool Save()
        {
            if (world == null || !world.CanSave) { Notify("Save available while safely exploring and idle."); return false; }
            bool success = Store.TrySave(world.Snapshot()); Notify(success ? "Progress saved" : Store.LastMessage); return success;
        }
        public void TogglePause()
        {
            if (State != GameFlowState.Playing) return;
            if (Paused && ActiveBonfire != null) { ActiveBonfire = null; Paused = false; world.ClearInput(); SetCursor(false); menu.Hide(); return; }
            Paused = !Paused; world.ClearInput(); SetCursor(Paused);
            if (!Paused) { menu.Hide(); return; }
            ShowPause();
        }
        void ShowPause()
        {
            menu.Show("PAUSED", world.CanSave ? "Safe exploration - saving is available." : "In combat / action: returning or quitting keeps the last safe save.",
                new MenuAction("Resume", TogglePause), new MenuAction("Save", () => { Save(); ShowPauseMessage(); }, world.CanSave),
                new MenuAction("Attributes", OpenAttributes), new MenuAction("Return to Home", ReturnHome), new MenuAction("Quit", Quit));
        }
        public void OpenBonfire(BonfireCheckpoint bonfire)
        {
            if (State != GameFlowState.Playing || bonfire == null) return;
            ActiveBonfire = bonfire; Paused = true; world.ClearInput(); SetCursor(true); ShowBonfire();
        }
        void ShowBonfire()
        {
            var growth = world.Progression;
            string healthPreview = growth.Vitality >= PlayerProgression.MaximumRank ? "已满" :
                (world.settings.player.maxHealth + 10 * (growth.Vitality + 1)).ToString("F0");
            string posturePreview = growth.Resolve >= PlayerProgression.MaximumRank ? "已满" :
                (world.settings.player.maxPosture + 10 * (growth.Resolve + 1)).ToString("F0");
            string powerPreview = growth.Power >= PlayerProgression.MaximumRank ? "已满" :
                (1f + .05f * (growth.Power + 1)).ToString("P0");
            menu.Show(ActiveBonfire.displayName,
                $"等级 {growth.Level}  经验 {growth.Experience}  下次消耗 {growth.NextCost}\n" +
                $"体魄 {growth.Vitality}/10 → 最大生命 {healthPreview}\n" +
                $"定力 {growth.Resolve}/10 → 最大架势 {posturePreview}\n" +
                $"攻击 {growth.Power}/10 → 伤害倍率 {powerPreview}",
                new MenuAction("体魄 +1", () => Upgrade(GrowthStat.Vitality), growth.CanUpgrade(GrowthStat.Vitality)),
                new MenuAction("定力 +1", () => Upgrade(GrowthStat.Resolve), growth.CanUpgrade(GrowthStat.Resolve)),
                new MenuAction("攻击 +1", () => Upgrade(GrowthStat.Power), growth.CanUpgrade(GrowthStat.Power)),
                new MenuAction("查看属性", OpenAttributes), new MenuAction("离开篝火", TogglePause));
        }
        void Upgrade(GrowthStat stat)
        {
            if (!world.TryUpgrade(stat)) { ShowBonfire(); Notify(Store.LastMessage); return; }
            ShowBonfire();
        }
        public void OpenAttributes()
        {
            if (State != GameFlowState.Playing) return;
            attributesFromBonfire = ActiveBonfire != null;
            Paused = true; world.ClearInput(); SetCursor(true);
            var growth = world.Progression;
            menu.Show("角色属性", $"等级 {growth.Level}  经验 {growth.Experience}  下次消耗 {growth.NextCost}\n" +
                $"体魄 {growth.Vitality}/10  生命 {world.player.Core.Health:F0}/{world.player.Core.Tuning.maxHealth:F0}\n" +
                $"定力 {growth.Resolve}/10  架势 {world.player.Core.Posture:F0}/{world.player.Core.Tuning.maxPosture:F0}\n" +
                $"攻击 {growth.Power}/10  伤害倍率 {growth.AttackMultiplier:P0}",
                new MenuAction("返回", () => { if (attributesFromBonfire) ShowBonfire(); else ShowPause(); }));
        }
        void ShowPauseMessage()
        { menu.Show("SAVE", Message, new MenuAction("Back", ShowPause)); }
        public void ReturnHome()
        {
            if (world != null && world.CanSave && !Save())
            {
                menu.Show("SAVE FAILED", Message, new MenuAction("Retry", ReturnHome),
                    new MenuAction("Leave Without Saving", HomeWithoutSave)); return;
            }
            HomeWithoutSave();
        }
        void HomeWithoutSave()
        {
            pending = null; BeginLoad("MainMenu");
        }
        public void PlayerDied()
        {
            State = GameFlowState.PlayerDead; world.ClearInput(); SetCursor(true);
            menu.Show("DEATH", "在最近篝火复活，保留成长与经验。", new MenuAction("Retry", Retry), new MenuAction("Return to Home", ReturnHome));
        }
        public void Retry()
        {
            var snapshot = world.DeathRespawnSnapshot();
            if (!Store.TrySave(snapshot)) Notify(Store.LastMessage);
            LoadLevel(snapshot);
        }
        public void Won()
        {
            world.ClearInput(); Save(); State = GameFlowState.Victory; SetCursor(true);
            menu.Show("VICTORY", "The guardian has fallen.\n" + Message,
                new MenuAction("Continue Exploring", ContinueExploring), new MenuAction("Return to Home", ReturnHome));
        }
        public void ContinueExploring() { State = GameFlowState.Playing; world.ClearInput(); menu.Hide(); SetCursor(false); }
        void Quit()
        {
            if (world != null && world.CanSave && !Save())
            {
                menu.Show("SAVE FAILED", Message, new MenuAction("Retry", Quit),
                    new MenuAction("Quit Without Saving", () => Application.Quit())); return;
            }
            Application.Quit();
        }
        void OnApplicationQuit() { if (world != null && Store != null && world.CanSave) Save(); }
        void OnDestroy() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
