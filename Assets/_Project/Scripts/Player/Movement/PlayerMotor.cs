using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    public float walkSpeed = 3f;
    public float moveSpeed = 5f;
    public float rotationSpeed = 720f;
    public float inputSmoothTime = 0.08f;
    public float accelerationTime = 0.12f;
    public float decelerationTime = 0.08f;
    public float gravity = -9.81f;
    public float jumpForce = 2f;
    public float animMaxSpeed = 5f;

    public float rollLandingMinImpact = 6f;
    public float hardLandingMinImpact = 12f;

    public float rollLandingLockSeconds = 0.35f;
    public float hardLandingLockSeconds = 0.55f;
    public float lockedMoveMultiplier = 0f;
    public float rollLandingPushSeconds = 0.25f;
    public float rollLandingPushSpeed = 4.5f;

    public float dodgeSpeed = 12f;
    public float dodgeDuration = 0.25f;
    public float dodgeIframeDuration = 0.25f;
    public float dodgeCooldown = 0.6f;

    private CharacterController controller;
    private Animator animator;
    private Vector3 velocity;

    private bool isGrounded;
    private bool wasGrounded;
    private float lastAirYVelocity;

    private float controlLockTimer;
    private float rollLandingPushTimer;
    private Vector3 rollLandingPushDir;
    private Vector3 lastMoveDir;

    private float dodgeTimer;
    private float iframeTimer;
    private float dodgeCooldownTimer;
    private Vector3 dodgeDirection;
    private bool isDodging;

    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction dodgeAction;

    private Vector2 moveInput;
    private Vector2 moveInputVelocity;

    private Vector3 horizontalVelocity;
    private Vector3 horizontalVelocityRef;

    private bool sprintHeld;
    private bool useSendMessages;
    private bool useActionCallbacks;
    private PlayerInput playerInput;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            useSendMessages = playerInput.notificationBehavior == PlayerNotifications.SendMessages
                || playerInput.notificationBehavior == PlayerNotifications.BroadcastMessages;
            useActionCallbacks = !useSendMessages;

            moveAction = playerInput.actions != null ? playerInput.actions.FindAction("Move", true) : null;
            jumpAction = playerInput.actions != null ? playerInput.actions.FindAction("Jump", true) : null;
            sprintAction = playerInput.actions != null ? playerInput.actions.FindAction("Sprint", false) : null;
            dodgeAction = playerInput.actions != null ? playerInput.actions.FindAction("Dodge", false) : null;
        }
    }

    void OnEnable()
    {
        if (useActionCallbacks && jumpAction != null)
        {
            jumpAction.performed += OnJump;
            jumpAction.Enable();
        }

        if (useActionCallbacks && moveAction != null)
        {
            moveAction.Enable();
        }

        if (useActionCallbacks && sprintAction != null)
        {
            sprintAction.Enable();
        }

        if (useActionCallbacks && dodgeAction != null)
        {
            dodgeAction.performed += OnDodge;
            dodgeAction.Enable();
        }
    }

    void OnDisable()
    {
        if (useActionCallbacks && jumpAction != null)
        {
            jumpAction.performed -= OnJump;
            jumpAction.Disable();
        }

        if (useActionCallbacks && moveAction != null)
        {
            moveAction.Disable();
        }

        if (useActionCallbacks && sprintAction != null)
        {
            sprintAction.Disable();
        }

        if (useActionCallbacks && dodgeAction != null)
        {
            dodgeAction.performed -= OnDodge;
            dodgeAction.Disable();
        }
    }

    void Update()
    {
        if (controller == null) return;

        if (controlLockTimer > 0f)
        {
            controlLockTimer -= Time.deltaTime;
            if (controlLockTimer < 0f) controlLockTimer = 0f;
        }

        if (rollLandingPushTimer > 0f)
        {
            rollLandingPushTimer -= Time.deltaTime;
            if (rollLandingPushTimer < 0f) rollLandingPushTimer = 0f;
        }

        if (dodgeTimer > 0f)
        {
            dodgeTimer -= Time.deltaTime;
            if (dodgeTimer < 0f) dodgeTimer = 0f;
        }
        else
        {
            isDodging = false;
        }

        if (iframeTimer > 0f)
        {
            iframeTimer -= Time.deltaTime;
            if (iframeTimer < 0f) iframeTimer = 0f;
        }

        if (dodgeCooldownTimer > 0f)
        {
            dodgeCooldownTimer -= Time.deltaTime;
            if (dodgeCooldownTimer < 0f) dodgeCooldownTimer = 0f;
        }

        wasGrounded = isGrounded;
        isGrounded = controller.isGrounded;

        Vector2 rawMoveInput = moveInput;

        if (useActionCallbacks)
            rawMoveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        moveInput = Vector2.SmoothDamp(moveInput, rawMoveInput, ref moveInputVelocity, inputSmoothTime);

        bool isSprinting;
        if (useActionCallbacks && sprintAction != null)
            isSprinting = sprintAction.IsPressed();
        else
            isSprinting = sprintHeld;

        Vector3 move = new Vector3(moveInput.x, 0f, moveInput.y);

        float inputMag = move.magnitude;
        if (move.sqrMagnitude > 0.01f)
            lastMoveDir = move.normalized;

        if (controlLockTimer > 0f)
        {
            move *= lockedMoveMultiplier;
            inputMag = move.magnitude;
            isSprinting = false;
        }

        if (move.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        float targetSpeed = isSprinting ? moveSpeed : walkSpeed;
        Vector3 targetHorizontalVelocity = inputMag > 0.0001f ? move.normalized * targetSpeed : Vector3.zero;

        float smoothTime = inputMag > 0.0001f ? accelerationTime : decelerationTime;
        horizontalVelocity = Vector3.SmoothDamp(horizontalVelocity, targetHorizontalVelocity, ref horizontalVelocityRef, smoothTime);

        controller.Move(horizontalVelocity * Time.deltaTime);

        if (rollLandingPushTimer > 0f)
        {
            controller.Move(rollLandingPushDir * rollLandingPushSpeed * Time.deltaTime);
        }

        if (isDodging && dodgeTimer > 0f)
        {
            controller.Move(dodgeDirection * dodgeSpeed * Time.deltaTime);
        }

        if (!isGrounded)
            lastAirYVelocity = velocity.y;

        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f;
        else if (!isGrounded)
            velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);

        float currentSpeed = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
        float speed01 = animMaxSpeed > 0f ? Mathf.Clamp01(currentSpeed / animMaxSpeed) : 0f;

        if (animator != null)
        {
            animator.SetBool("IsGrounded", isGrounded);
            animator.SetFloat("Speed", speed01, 0.08f, Time.deltaTime);
            animator.SetFloat("YVelocity", velocity.y);
        }

        if (!wasGrounded && isGrounded)
        {
            float impact = Mathf.Abs(lastAirYVelocity);
            int landType = 0;

            if (impact >= hardLandingMinImpact) landType = 2;
            else if (impact >= rollLandingMinImpact) landType = 1;

            if (landType == 2) controlLockTimer = Mathf.Max(controlLockTimer, hardLandingLockSeconds);
            else if (landType == 1)
            {
                controlLockTimer = Mathf.Max(controlLockTimer, rollLandingLockSeconds);

                rollLandingPushTimer = rollLandingPushSeconds;
                rollLandingPushDir = lastMoveDir.sqrMagnitude > 0.01f ? lastMoveDir : transform.forward;
            }

            if (animator != null)
            {
                animator.SetInteger("LandType", landType);
            }
        }
    }

    void OnJump(InputAction.CallbackContext context)
    {
        PerformJump();
    }

    void OnMove(InputValue value)
    {
        if (useSendMessages)
            moveInput = value.Get<Vector2>();
    }

    void OnJump(InputValue value)
    {
        if (!useSendMessages || !value.isPressed) return;
        PerformJump();
    }

    void OnSprint(InputValue value)
    {
        if (useSendMessages)
            sprintHeld = value.isPressed;
    }

    void OnDodge(InputAction.CallbackContext context)
    {
        PerformDodge();
    }

    void OnDodge(InputValue value)
    {
        if (useSendMessages && value.isPressed)
        {
            PerformDodge();
        }
    }

    void PerformDodge()
    {
        if (dodgeCooldownTimer > 0f) return;
        if (isDodging) return;

        isDodging = true;
        dodgeTimer = dodgeDuration;
        iframeTimer = dodgeIframeDuration;
        dodgeCooldownTimer = dodgeCooldown;
        // Locar todos os controles durante o roll
        controlLockTimer = dodgeDuration;

        dodgeDirection = lastMoveDir.sqrMagnitude > 0.01f ? lastMoveDir : transform.forward;

        if (animator != null)
        {
            animator.SetTrigger("Roll");
        }
    }

    public bool IsInvulnerable() => iframeTimer > 0f;
    public bool IsControlLocked() => controlLockTimer > 0f;

    void PerformJump()
    {
        if (controlLockTimer > 0f) return;
        if (!isGrounded) return;

        velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);

        if (animator != null)
            animator.SetTrigger("Jump");
    }
}