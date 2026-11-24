using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(PlayerInput), typeof(CapsuleCollider))]
public class PlayerFPS : MonoBehaviour
{
    public float speed = 5f;
    public float sensitivity = 0.15f;
    public Transform playerCamera;
    public float jumpForce = 5f;

    [SerializeField] private float crouchHeight = 1.38f;
    [SerializeField] private float crouchSpeed = 2f;
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private AudioSource salsaSource;
    [SerializeField] private Vector3 thirdPersonOffset = new Vector3(0.45f, 0.22f, -2.8f);
    [SerializeField] private float interactDistance = 2.5f;

    private static readonly int Moving = Animator.StringToHash("Moving");
    private static readonly int Grounded = Animator.StringToHash("Grounded");
    private static readonly int Crouched = Animator.StringToHash("Crouched");
    private static readonly int Dance = Animator.StringToHash("Dance");

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private InputAction moveAction, lookAction, jumpAction, crouchAction, danceAction, viewAction, interactAction;
    private Vector2 moveInput;
    private Vector3 standingCenter, standingCameraPosition;
    private Camera viewCamera;
    private int firstPersonMask, headMask;
    private float eyeHeight;
    private float standingHeight, yaw, pitch;
    private bool crouched, jumpRequested, danceRequested, dancing, thirdPerson;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        standingHeight = capsule.height;
        standingCenter = capsule.center;
        standingCameraPosition = playerCamera.localPosition;
        eyeHeight = standingCameraPosition.y;
        viewCamera = playerCamera.GetComponent<Camera>();
        firstPersonMask = viewCamera.cullingMask;
        headMask = LayerMask.GetMask("LocalPlayerHead");
        var actions = GetComponent<PlayerInput>().actions;
        moveAction = actions.FindAction("Player/Move", true);
        lookAction = actions.FindAction("Player/Look", true);
        jumpAction = actions.FindAction("Player/Jump", true);
        crouchAction = actions.FindAction("Player/Crouch", true);
        danceAction = actions.FindAction("Player/Dance", true);
        viewAction = actions.FindAction("Player/ToggleView", true);
        interactAction = actions.FindAction("Player/Interact", true);
        yaw = transform.eulerAngles.y;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>();
        jumpRequested |= jumpAction.WasPressedThisFrame();
        danceRequested |= danceAction.WasPressedThisFrame();
        if (viewAction.WasPressedThisFrame())
            thirdPerson = !thirdPerson;
        if (interactAction.WasPressedThisFrame())
            TryInteract();

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 look = lookAction.ReadValue<Vector2>();
            yaw += look.x * sensitivity;
            pitch = Mathf.Clamp(pitch - look.y * sensitivity, -80f, 80f);
        }
    }

    void LateUpdate()
    {
        float targetHeight = Mathf.Min(standingCameraPosition.y, capsule.center.y + capsule.height * 0.5f - 0.12f);
        eyeHeight = Mathf.MoveTowards(eyeHeight, targetHeight, 3.5f * Time.deltaTime);
        Vector3 pivot = standingCameraPosition;
        pivot.y = eyeHeight;
        playerCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        playerCamera.localPosition = pivot;
        viewCamera.cullingMask = firstPersonMask;
        if (!thirdPerson)
            return;

        // Start inside the player's collider so the sweep only hits the environment.
        Vector3 offset = playerCamera.rotation * thirdPersonOffset;
        Vector3 direction = offset.normalized;
        float distance = offset.magnitude;
        float nearHeight = viewCamera.nearClipPlane * Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
        float radius = Mathf.Max(0.16f, nearHeight * Mathf.Sqrt(1f + viewCamera.aspect * viewCamera.aspect));
        if (Physics.SphereCast(playerCamera.position, radius, direction, out RaycastHit hit,
            distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            distance = Mathf.Max(0f, hit.distance - 0.04f);

        playerCamera.position += direction * distance;
        if (distance > 0.45f)
            viewCamera.cullingMask |= headMask;
    }

    void FixedUpdate()
    {
        bool grounded = rb.linearVelocity.y < 0.5f && Physics.Raycast(
            rb.position + standingCenter, Vector3.down, standingHeight * 0.5f + 0.08f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        bool wantsCrouch = crouchAction.IsPressed() && grounded;
        if (wantsCrouch != crouched && (wantsCrouch || CanStand()))
        {
            crouched = wantsCrouch;
            capsule.height = crouched ? Mathf.Clamp(crouchHeight, capsule.radius * 2f, standingHeight) : standingHeight;
            capsule.center = standingCenter + Vector3.down * ((standingHeight - capsule.height) * 0.5f);
        }

        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        rb.MoveRotation(rotation);
        Vector3 direction = Vector3.ClampMagnitude(new Vector3(moveInput.x, 0f, moveInput.y), 1f);
        Vector3 velocity = rotation * direction * (crouched ? crouchSpeed : speed);
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;
        if (jumpRequested && grounded && !crouched)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            grounded = false;
        }

        if (characterAnimator != null)
        {
            bool moving = direction.sqrMagnitude > 0.001f;
            characterAnimator.SetBool(Moving, moving);
            characterAnimator.SetBool(Grounded, grounded);
            characterAnimator.SetBool(Crouched, crouched);
            bool canDance = grounded && !moving && !crouched;
            SetDancing(canDance && (danceRequested ? !dancing : dancing));
        }
        jumpRequested = false;
        danceRequested = false;
    }

    private void SetDancing(bool active)
    {
        if (active == dancing)
            return;
        dancing = active;
        characterAnimator.SetBool(Dance, active);
        if (salsaSource != null)
        {
            if (active) salsaSource.Play();
            else salsaSource.Stop();
        }
    }

    void OnDisable()
    {
        SetDancing(false);
        danceRequested = false;
        jumpRequested = false;
    }

    private void TryInteract()
    {
        // Rayo desde los ojos (no desde la cámara en tercera persona); empieza dentro de la cápsula, así que no la golpea.
        Vector3 eyes = transform.TransformPoint(new Vector3(standingCameraPosition.x, eyeHeight, standingCameraPosition.z));
        if (Physics.Raycast(eyes, playerCamera.forward, out RaycastHit hit, interactDistance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            var door = hit.collider.GetComponentInParent<Door>();
            if (door != null) door.Toggle();
        }
    }

    private bool CanStand()
    {
        // Sweep only the additional headroom, keeping the bottom of the capsule on the floor.
        Vector3 top = rb.position + capsule.center + Vector3.up * (capsule.height * 0.5f - capsule.radius);
        return !Physics.SphereCast(top, capsule.radius - 0.01f, Vector3.up, out _,
            standingHeight - capsule.height + 0.01f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }
}
