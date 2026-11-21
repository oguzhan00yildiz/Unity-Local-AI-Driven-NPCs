using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace AISystem
{
    /// <summary>
    /// Reads a KeyCode with the Input System package or the old Input Manager, whichever is active.
    /// Supports letters A to Z, Space, Return, Tab and Escape with the Input System.
    /// </summary>
    public static class KeyInput
    {
        public static bool WasPressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var control = GetControl(key);
            return control != null && control.wasPressedThisFrame;
#else
            return Input.GetKeyDown(key);
#endif
        }

        public static bool WasReleased(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var control = GetControl(key);
            return control != null && control.wasReleasedThisFrame;
#else
            return Input.GetKeyUp(key);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static KeyControl GetControl(KeyCode key)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return null;

            if (key >= KeyCode.A && key <= KeyCode.Z)
                return keyboard[Key.A + (key - KeyCode.A)];

            return key switch
            {
                KeyCode.Space  => keyboard.spaceKey,
                KeyCode.Return => keyboard.enterKey,
                KeyCode.Tab    => keyboard.tabKey,
                KeyCode.Escape => keyboard.escapeKey,
                _ => null
            };
        }
#endif
    }
}
