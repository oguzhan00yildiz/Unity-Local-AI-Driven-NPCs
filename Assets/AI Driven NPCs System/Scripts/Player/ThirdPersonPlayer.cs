using UnityEngine;

namespace AISystem
{
    /// <summary>
    /// Third-person character controller.
    /// No Cinemachine, no Starter Assets, no PlayerInput component required.
    ///
    /// Scene setup
    /// ───────────
    ///  1. Add CharacterController, PlayerInputHandler, ThirdPersonPlayer to the player root.
    ///  2. Add your visual mesh/capsule as a child.
    ///  3. Place Camera.main anywhere you like — this script does NOT move it.
    ///     WASD moves the character relative to Camera.main's facing direction.
    ///
    /// Controls
    /// ────────
    ///  WASD        → move relative to camera direction
    ///  Left Shift  → sprint
    ///  Space       → jump
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonPlayer : MonoBehaviour
    {
    // ── Movement ─────────────────────────────────────────────────────────────

    [Header("Movement")]
    public float walkSpeed          = 3f;
    public float sprintSpeed        = 6f;
    public float speedSmoothTime    = 0.1f;
    [Range(0f, 0.3f)]
    public float turnSmoothTime     = 0.1f;

    // ── Jump & Gravity ────────────────────────────────────────────────────────

    [Header("Jump & Gravity")]
    public float jumpHeight         = 1.2f;
    public float gravity            = -20f;
    public float jumpCooldown       = 0.4f;
    public float fallTimeout        = 0.2f;

    // ── Ground Detection ──────────────────────────────────────────────────────

    [Header("Ground Detection")]
    public LayerMask groundLayers;
    public float     groundedOffset = -0.14f;
    public float     groundedRadius = 0.3f;

    // ── Visual ────────────────────────────────────────────────────────────────

    [Header("Visual")]
    [Tooltip("Child transform that visually represents the character. Auto-found if left empty.")]
    public Transform visualBody;

    // ── Audio ─────────────────────────────────────────────────────────────────

    [Header("Audio")]
    public AudioClip   landingClip;
    public AudioClip[] footstepClips;
    [Range(0f, 1f)]
    public float footstepVolume = 0.5f;

    // ── Private state ─────────────────────────────────────────────────────────

    // Movement
    private float _currentSpeed;
    private float _speedVelocity;
    private float _turnVelocity;
    private float _verticalVelocity;
    private const float _terminalVelocity = 53f;

    // Grounded state
    private bool _grounded;

    // Jump timers
    private float _jumpCooldownTimer;
    private float _fallTimer;

    // Animator
    private int  _idSpeed;
    private int  _idGrounded;
    private int  _idJump;
    private int  _idFreeFall;
    private int  _idMotionSpeed;
    private bool _hasAnimator;

    // Interaction lock (NPC chat)
    private bool _movementEnabled = true;

    // Cached components / transforms
    private CharacterController _controller;
    private PlayerInputHandler  _input;
    private Animator            _animator;
    private Transform           _cam;    // Camera.main — read-only, for movement direction
    private Transform           _visual;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        _controller  = GetComponent<CharacterController>();
        _input       = GetComponent<PlayerInputHandler>();
        _animator    = GetComponentInChildren<Animator>();
        _hasAnimator = _animator != null;

        if (Camera.main != null) _cam = Camera.main.transform;

        if (_input == null)
        {
            Debug.LogError("[ThirdPersonPlayer] PlayerInputHandler not found.", this);
            enabled = false;
            return;
        }

        // Resolve visual body: explicit field → first child Renderer → null (root only)
        if (visualBody != null)
            _visual = visualBody;
        else
        {
            Renderer r = GetComponentInChildren<Renderer>();
            if (r != null) _visual = r.transform;
        }

        AssignAnimationIDs();
        _jumpCooldownTimer = jumpCooldown;
        _fallTimer         = fallTimeout;
    }

    private void Update()
    {
        CheckGround();
        ApplyGravity();
        HandleJump();
        HandleMovement();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Freeze/unfreeze movement. Called by NPC chat UI.</summary>
    public void SetMovementEnabled(bool value)
    {
        _movementEnabled = value;
        if (_input != null) _input.CursorLocked = value;
    }

    // ── Ground Detection ──────────────────────────────────────────────────────

    private void CheckGround()
    {
        Vector3 probePos = new Vector3(
            transform.position.x,
            transform.position.y - groundedOffset,
            transform.position.z);

        _grounded = Physics.CheckSphere(probePos, groundedRadius,
            groundLayers, QueryTriggerInteraction.Ignore);

        if (_hasAnimator)
            _animator.SetBool(_idGrounded, _grounded);
    }

    // ── Gravity ───────────────────────────────────────────────────────────────

    private void ApplyGravity()
    {
        if (_grounded)
        {
            _fallTimer = fallTimeout;
            if (_hasAnimator)
            {
                _animator.SetBool(_idJump,     false);
                _animator.SetBool(_idFreeFall, false);
            }
            if (_verticalVelocity < 0f) _verticalVelocity = -2f;
        }
        else
        {
            _fallTimer -= Time.deltaTime;
            if (_fallTimer <= 0f && _hasAnimator)
                _animator.SetBool(_idFreeFall, true);
        }

        if (_verticalVelocity < _terminalVelocity)
            _verticalVelocity += gravity * Time.deltaTime;
    }

    // ── Jump ──────────────────────────────────────────────────────────────────

    private void HandleJump()
    {
        if (_grounded)
        {
            _jumpCooldownTimer -= Time.deltaTime;

            if (_movementEnabled && _input.Jump && _jumpCooldownTimer <= 0f)
            {
                _verticalVelocity  = Mathf.Sqrt(jumpHeight * -2f * gravity);
                _jumpCooldownTimer = jumpCooldown;
                if (_hasAnimator) _animator.SetBool(_idJump, true);
            }
            _input.ConsumeJump();
        }
        else
        {
            _jumpCooldownTimer = jumpCooldown;
            _input.ConsumeJump();
        }
    }

    // ── Movement ──────────────────────────────────────────────────────────────

    private void HandleMovement()
    {
        Vector2 input      = _movementEnabled ? _input.Move : Vector2.zero;
        bool    sprinting  = _movementEnabled && _input.Sprint;
        float   targetSpd  = input.sqrMagnitude > 0.01f
                             ? (sprinting ? sprintSpeed : walkSpeed)
                             : 0f;

        // Smooth speed.
        _currentSpeed = Mathf.SmoothDamp(
            _currentSpeed, targetSpd, ref _speedVelocity, speedSmoothTime);

        if (_currentSpeed < 0.01f) _currentSpeed = 0f;

        // World-space move direction: WASD relative to camera's horizontal facing.
        Vector3 moveDir = Vector3.zero;
        if (input.sqrMagnitude > 0.01f)
        {
            // Flatten camera's forward and right onto the XZ plane.
            Vector3 camForward = _cam != null ? _cam.forward : Vector3.forward;
            Vector3 camRight   = _cam != null ? _cam.right   : Vector3.right;
            camForward.y = 0f; camForward.Normalize();
            camRight.y   = 0f; camRight.Normalize();

            moveDir = (camForward * input.y + camRight * input.x).normalized;

            // Rotate the visual body smoothly toward movement direction.
            if (_visual != null)
            {
                float targetAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
                float smoothAngle = Mathf.SmoothDampAngle(
                    _visual.eulerAngles.y, targetAngle, ref _turnVelocity, turnSmoothTime);
                _visual.rotation = Quaternion.Euler(0f, smoothAngle, 0f);
            }
        }

        // Apply movement + gravity.
        Vector3 velocity = moveDir * _currentSpeed + Vector3.up * _verticalVelocity;
        _controller.Move(velocity * Time.deltaTime);

        if (_hasAnimator)
        {
            _animator.SetFloat(_idSpeed,       _currentSpeed);
            _animator.SetFloat(_idMotionSpeed, input.magnitude);
        }
    }

    // ── Animator IDs ─────────────────────────────────────────────────────────

    private void AssignAnimationIDs()
    {
        _idSpeed       = Animator.StringToHash("Speed");
        _idGrounded    = Animator.StringToHash("Grounded");
        _idJump        = Animator.StringToHash("Jump");
        _idFreeFall    = Animator.StringToHash("FreeFall");
        _idMotionSpeed = Animator.StringToHash("MotionSpeed");
    }

    // ── Audio (Animation Events) ──────────────────────────────────────────────

    private void OnFootstep(AnimationEvent animEvent)
    {
        if (animEvent.animatorClipInfo.weight > 0.5f && footstepClips.Length > 0)
            AudioSource.PlayClipAtPoint(
                footstepClips[Random.Range(0, footstepClips.Length)],
                transform.position, footstepVolume);
    }

    private void OnLand(AnimationEvent animEvent)
    {
        if (animEvent.animatorClipInfo.weight > 0.5f && landingClip != null)
            AudioSource.PlayClipAtPoint(landingClip, transform.position, footstepVolume);
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Ground probe sphere.
        Gizmos.color = _grounded ? new Color(0f, 1f, 0f, 0.4f) : new Color(1f, 0f, 0f, 0.4f);
        Gizmos.DrawSphere(
            new Vector3(transform.position.x, transform.position.y - groundedOffset, transform.position.z),
            groundedRadius);
    }
}
}
