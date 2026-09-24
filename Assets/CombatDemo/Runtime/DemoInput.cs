using UnityEngine;
using UnityEngine.InputSystem;

namespace Milkfrog.CombatDemo
{
    public struct CombatInputFrame
    {
        public Vector2 move;
        public bool attackPressed,attackHeld,attackReleased,guardHeld,guardPressed,dodgePressed,resetPressed,lockPressed;
    }
    public sealed class DemoInput : System.IDisposable
    {
        readonly InputActionMap map = new InputActionMap("CombatDemo");
        readonly InputAction move, attack, guard, reset, mode, dodge, lockOn, look, pause, interact, attributes;
        public DemoInput(string lockBinding = "<Keyboard>/tab")
        {
            move = map.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            attack = map.AddAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
            guard = map.AddAction("Guard", InputActionType.Button, "<Mouse>/rightButton");
            reset = map.AddAction("Reset", InputActionType.Button, "<Keyboard>/r");
            mode = map.AddAction("NextMode", InputActionType.Button, "<Keyboard>/f1");
            dodge = map.AddAction("Dodge",InputActionType.Button,"<Keyboard>/space");
            var lockOn = map.AddAction("Lock",InputActionType.Button,lockBinding);
            this.lockOn = lockOn;
            look = map.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            pause = map.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            interact = map.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            attributes = map.AddAction("Attributes", InputActionType.Button, "<Keyboard>/c");
            map.Enable();
        }
        public Vector2 Move => move.ReadValue<Vector2>();
        public Vector2 Look => look.ReadValue<Vector2>();
        public bool PausePressed => pause.WasPressedThisFrame();
        public bool InteractPressed => interact.WasPressedThisFrame();
        public bool AttributesPressed => attributes.WasPressedThisFrame();
        public bool AttackPressed => attack.WasPressedThisFrame();
        public bool GuardHeld => guard.IsPressed();
        public bool GuardPressed => guard.WasPressedThisFrame();
        public bool ResetPressed => reset.WasPressedThisFrame();
        public bool ModePressed => mode.WasPressedThisFrame();
        public CombatInputFrame Snapshot => new CombatInputFrame {move=Move,attackPressed=AttackPressed,attackHeld=attack.IsPressed(),attackReleased=attack.WasReleasedThisFrame(),guardHeld=GuardHeld,guardPressed=GuardPressed,dodgePressed=dodge.WasPressedThisFrame(),resetPressed=ResetPressed,lockPressed=lockOn.WasPressedThisFrame()};
        public void Dispose() => map.Dispose();
    }
}
