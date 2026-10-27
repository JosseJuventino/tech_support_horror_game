
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(PlayerInput))]
public class PlayerFPS : MonoBehaviour
{
    [Header("Configuración")]
    public float speed = 5f;
    public float sensitivity = 0.15f;
    public Transform playerCamera;

    private Rigidbody rb;
    private InputAction moveAction;
    private InputAction lookAction;

    private Vector2 moveInput;
    private float pitch;
    private float yaw;

    public float jumpForce = 5f;

    private InputAction jumpAction;
    private bool jumpRequested;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        PlayerInput input = GetComponent<PlayerInput>();

        moveAction = input.actions.FindAction("Move", true);
        lookAction = input.actions.FindAction("Look", true);
        jumpAction = input.actions.FindAction("Jump", true);

        yaw = transform.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>();

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (jumpAction.WasPressedThisFrame())
        {
            jumpRequested = true;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        Vector2 look = lookAction.ReadValue<Vector2>();

        yaw += look.x * sensitivity;
        pitch -= look.y * sensitivity;

        pitch = Mathf.Clamp(pitch, -80f, 80f);

        playerCamera.localRotation =
            Quaternion.Euler(pitch, 0f, 0f);
    }

    void FixedUpdate()
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        rb.MoveRotation(rotation);

        Vector3 direction = rotation *
            new Vector3(moveInput.x, 0f, moveInput.y);

        direction = Vector3.ClampMagnitude(direction, 1f);

        Vector3 velocity = rb.linearVelocity;

        rb.linearVelocity = new Vector3(
            direction.x * speed,
            velocity.y,
            direction.z * speed
        );

        bool isGrounded = Physics.Raycast(
            transform.position,
            Vector3.down,
            1.1f
        );

        if (jumpRequested && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        jumpRequested = false;
    }
}
