using UnityEngine;
using UnityEngine.InputSystem;

namespace Milkfrog.CombatDemo
{
    public struct CombatInputFrame
    {
        public Vector2 move;
        public bool attackPressed,attackHeld,attackReleased,guardHeld,guardPressed,dodgePressed,jumpPressed,resetPressed,lockPressed;
        public bool itemPressed, itemHeld, itemReleased;
        public bool slot1Pressed, slot2Pressed, slot3Pressed, slot1Held, slot2Held, slot3Held;
    }
    public sealed class DemoInput : System.IDisposable
    {
        readonly InputActionMap map = new InputActionMap("CombatDemo");
        readonly InputAction move, attack, guard, reset, mode, dodge, jump, lockOn, look, pause, interact, attributes, inventory, quickUse;
        readonly InputAction slot1, slot2, slot3;
        public DemoInput(string lockBinding = "<Keyboard>/tab")
        {
            move = map.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            attack = map.AddAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
            guard = map.AddAction("Guard", InputActionType.Button, "<Mouse>/rightButton");
            reset = map.AddAction("Reset", InputActionType.Button, "<Keyboard>/r");
            mode = map.AddAction("NextMode", InputActionType.Button, "<Keyboard>/f1");
            dodge = map.AddAction("Dodge",InputActionType.Button,"<Keyboard>/leftShift");
            jump = map.AddAction("Jump",InputActionType.Button,"<Keyboard>/space");
            var lockOn = map.AddAction("Lock",InputActionType.Button,lockBinding);
            this.lockOn = lockOn;
            look = map.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            pause = map.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            interact = map.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            attributes = map.AddAction("Attributes", InputActionType.Button, "<Keyboard>/c");
            inventory = map.AddAction("Inventory", InputActionType.Button, "<Keyboard>/tab");
            quickUse = map.AddAction("QuickUse", InputActionType.Button, "<Keyboard>/q");
            slot1 = map.AddAction("QuickSlot1", InputActionType.Button, "<Keyboard>/1");
            slot2 = map.AddAction("QuickSlot2", InputActionType.Button, "<Keyboard>/2");
            slot3 = map.AddAction("QuickSlot3", InputActionType.Button, "<Keyboard>/3");
            map.Enable();
        }
        public Vector2 Move => move.ReadValue<Vector2>();
        public Vector2 Look => look.ReadValue<Vector2>();
        public bool PausePressed => pause.WasPressedThisFrame();
        public bool InteractPressed => interact.WasPressedThisFrame();
        public bool AttributesPressed => attributes.WasPressedThisFrame();
        public bool InventoryPressed => inventory.WasPressedThisFrame();
        public bool AttackPressed => attack.WasPressedThisFrame();
        public bool GuardHeld => guard.IsPressed();
        public bool GuardPressed => guard.WasPressedThisFrame();
        public bool ResetPressed => reset.WasPressedThisFrame();
        public bool ModePressed => mode.WasPressedThisFrame();
        public CombatInputFrame Snapshot => new CombatInputFrame {move=Move,attackPressed=AttackPressed,attackHeld=attack.IsPressed(),attackReleased=attack.WasReleasedThisFrame(),guardHeld=GuardHeld,guardPressed=GuardPressed,dodgePressed=dodge.WasPressedThisFrame(),jumpPressed=jump.WasPressedThisFrame(),resetPressed=ResetPressed,lockPressed=lockOn.WasPressedThisFrame(),itemPressed=quickUse.WasPressedThisFrame(),itemHeld=quickUse.IsPressed(),itemReleased=quickUse.WasReleasedThisFrame(),slot1Pressed=slot1.WasPressedThisFrame(),slot2Pressed=slot2.WasPressedThisFrame(),slot3Pressed=slot3.WasPressedThisFrame(),slot1Held=slot1.IsPressed(),slot2Held=slot2.IsPressed(),slot3Held=slot3.IsPressed()};
        public void Dispose() => map.Dispose();
    }
}
