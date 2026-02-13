using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float gravity = -9.81f;
    public float jumpForce = 2f;

    private CharacterController controller;
    private Animator animator;
    private Vector3 velocity;
    private bool isGrounded;
    private InputAction moveAction;
    private InputAction jumpAction;
    private Vector2 moveInput;
    private bool useSendMessages;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        var playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            useSendMessages = playerInput.notificationBehavior == PlayerNotifications.SendMessages
                || playerInput.notificationBehavior == PlayerNotifications.BroadcastMessages;
            moveAction = playerInput.actions["Move"];
            jumpAction = playerInput.actions["Jump"];
        }
    }

    void OnEnable()
    {
        if (!useSendMessages && jumpAction != null)
        {
            jumpAction.performed += OnJump;
            jumpAction.Enable();
        }
        if (!useSendMessages && moveAction != null)
        {
            moveAction.Enable();
        }
    }

    void OnDisable()
    {
        if (!useSendMessages && jumpAction != null)
        {
            jumpAction.performed -= OnJump;
            jumpAction.Disable();
        }
        if (!useSendMessages && moveAction != null)
        {
            moveAction.Disable();
        }
    }

    void Update()
    {
        isGrounded = controller.isGrounded;
        animator.SetBool("IsGrounded", isGrounded);

        if (!useSendMessages && moveAction != null)
        {
            moveInput = moveAction.ReadValue<Vector2>();
        }

        Vector3 move = new Vector3(moveInput.x, 0f, moveInput.y);
        float speedPercent = Mathf.Clamp01(move.magnitude);

        if (move.magnitude > 0.1f)
        {
            transform.forward = move.normalized;
        }

        controller.Move(move.normalized * moveSpeed * Time.deltaTime);

        animator.SetFloat("Speed", speedPercent);

        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void OnJump(InputAction.CallbackContext context)
    {
        if (isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            animator.SetTrigger("Jump");
        }
    }

    void OnMove(InputValue value)
    {
        if (useSendMessages)
        {
            moveInput = value.Get<Vector2>();
        }
    }

    void OnJump(InputValue value)
    {
        if (!useSendMessages || !value.isPressed)
        {
            return;
        }

        if (isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            animator.SetTrigger("Jump");
        }
    }
}
