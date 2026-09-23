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
            Paused = !Paused; world.ClearInput(); SetCursor(Paused);
            if (!Paused) { menu.Hide(); return; }
            ShowPause();
        }
        void ShowPause()
        {
            menu.Show("PAUSED", world.CanSave ? "Safe exploration - saving is available." : "In combat / action: returning or quitting keeps the last safe save.",
                new MenuAction("Resume", TogglePause), new MenuAction("Save", () => { Save(); ShowPauseMessage(); }, world.CanSave),
                new MenuAction("Return to Home", ReturnHome), new MenuAction("Quit", Quit));
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
            menu.Show("DEATH", "Return to your last safe save.", new MenuAction("Retry", Retry), new MenuAction("Return to Home", ReturnHome));
        }
        public void Retry()
        {
            if (Store.TryLoad(out var snapshot)) LoadLevel(snapshot);
            else menu.Show("SAVE UNAVAILABLE", Store.LastMessage, new MenuAction("Return to Home", ReturnHome));
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
