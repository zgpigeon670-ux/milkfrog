using UnityEngine;
using UnityEngine.InputSystem;

namespace Milkfrog.CombatDemo
{
    public sealed class DemoInput : System.IDisposable
    {
        readonly InputActionMap map = new InputActionMap("CombatDemo");
        readonly InputAction move, attack, guard, reset, mode;
        public DemoInput()
        {
            move = map.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            attack = map.AddAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
            guard = map.AddAction("Guard", InputActionType.Button, "<Mouse>/rightButton");
            reset = map.AddAction("Reset", InputActionType.Button, "<Keyboard>/r");
            mode = map.AddAction("NextMode", InputActionType.Button, "<Keyboard>/f1");
            map.Enable();
        }
        public Vector2 Move => move.ReadValue<Vector2>();
        public bool AttackPressed => attack.WasPressedThisFrame();
        public bool GuardHeld => guard.IsPressed();
        public bool GuardPressed => guard.WasPressedThisFrame();
        public bool ResetPressed => reset.WasPressedThisFrame();
        public bool ModePressed => mode.WasPressedThisFrame();
        public void Dispose() => map.Dispose();
    }
}
