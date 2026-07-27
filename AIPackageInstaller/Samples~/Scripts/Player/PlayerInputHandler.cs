using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Reads raw input every frame and exposes it as simple properties.
/// Works with both the New Input System and the Legacy Input Manager.
/// No PlayerInput component needed — drop on the same GameObject as ThirdPersonPlayer.
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    // ── Exposed state ────────────────────────────────────────────────────────

    public Vector2 Move   { get; private set; }
    public bool    Jump   { get; private set; }
    public bool    Sprint { get; private set; }

    [Header("Cursor")]
    [Tooltip("Lock the cursor to the centre of the screen on start.")]
    public bool lockCursorOnStart = true;

    /// <summary>
    /// Set false to release the cursor (e.g. while NPC chat is open).
    /// ThirdPersonPlayer calls this automatically via SetMovementEnabled().
    /// </summary>
    public bool CursorLocked
    {
        get => _cursorLocked;
        set { _cursorLocked = value; ApplyCursorState(); }
    }

    private bool _cursorLocked;

    // ── Unity messages ───────────────────────────────────────────────────────

    private void Start()
    {
        CursorLocked = lockCursorOnStart;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) ApplyCursorState();
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        PollNewInputSystem();
#else
        PollLegacyInputSystem();
#endif
    }

    // ── New Input System — direct polling (no PlayerInput component needed) ──

#if ENABLE_INPUT_SYSTEM
    private void PollNewInputSystem()
    {
        var kb = Keyboard.current;

        if (kb == null) return;

        // WASD / Arrow keys → Move
        float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                - (kb.aKey.isPressed || kb.leftArrowKey.isPressed  ? 1f : 0f);
        float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed    ? 1f : 0f)
                - (kb.sKey.isPressed || kb.downArrowKey.isPressed   ? 1f : 0f);
        Move = new Vector2(x, y);

        // Left Shift → Sprint
        Sprint = kb.leftShiftKey.isPressed;

        // Space → Jump (latched until ConsumeJump is called)
        if (kb.spaceKey.wasPressedThisFrame)
            Jump = true;
    }
#endif

    // ── Legacy Input Manager polling ─────────────────────────────────────────

#if !ENABLE_INPUT_SYSTEM
    private void PollLegacyInputSystem()
    {
        Move   = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Sprint = Input.GetKey(KeyCode.LeftShift);

        if (Input.GetButtonDown("Jump"))
            Jump = true;
    }
#endif

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Consume the jump flag — call once per frame after processing it.</summary>
    public void ConsumeJump() => Jump = false;

    private void ApplyCursorState()
    {
        Cursor.lockState = _cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !_cursorLocked;
    }
}
