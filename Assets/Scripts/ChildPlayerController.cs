using UnityEngine;

// First-person controller for the child main character.
// WASD / arrow keys = move, Space = jump, Left Shift = sprint, C or Left Ctrl = crouch, V = switch first/third person, Mouse = look.
[RequireComponent(typeof(CharacterController))]
public class ChildPlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2.2f;
    [SerializeField] private float sprintSpeed = 4.0f;
    [SerializeField] private float acceleration = 12f; 
    [SerializeField] private float jumpHeight = 0.6f;
    [SerializeField] private float gravity = -18f;
    [SerializeField] private float coyoteTime = 0.1f;  

    [Header("Crouch")]
    [SerializeField] private KeyCode crouchKey = KeyCode.C;
    [SerializeField] private KeyCode altCrouchKey = KeyCode.LeftControl;
    [SerializeField] private bool holdToCrouch = true;
    [SerializeField] private float crouchHeight = 0.76f;
    [SerializeField] private float crouchSpeed = 1.3f;
    [SerializeField] private float crouchCameraHeight = 0.57f;
    [SerializeField] private float crouchCameraForward = 0.20f;
    [SerializeField] private float crouchTransitionSpeed = 8f;
    [SerializeField] private Transform bodyVisual;

    [Header("Mouse Look")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 80f;

    [Header("Camera View")]
    [SerializeField] private KeyCode switchViewKey = KeyCode.V;
    [SerializeField] private bool startInThirdPerson = true;
    [SerializeField] private Vector3 thirdPersonOffset = new Vector3(0.3f, 0.12f, -1.4f);
    [SerializeField] private float cameraCollisionRadius = 0.15f;

    [Header("Spawn")]
    [SerializeField] private bool snapToFloorOnStart = true;
    [SerializeField] private string fallbackFloorName = "Ground";
    [SerializeField] private float fallResetHeight = -20f;
    [SerializeField] private bool disableOtherMainCameras = true;

    private CharacterController controller;
    private PlayerStamina stamina;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float pitch;
    private float lastGroundedTime = -10f;
    private Vector3 spawnPoint;
    private float standHeight;
    private float standCameraHeight;
    private float crouchAmount;
    private bool crouchToggled;
    private float standCameraForward;
    private Vector3 cameraBase;      

    public bool IsGrounded => controller != null && controller.isGrounded;
    public bool IsMoving { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsCrouching { get; private set; }
    public float CrouchAmount => crouchAmount;
    public bool IsThirdPerson { get; private set; }
    public bool IsGroundedStable => Time.time - lastGroundedTime < 0.15f;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        stamina = GetComponent<PlayerStamina>();

        standHeight = controller.height;
        if (cameraPivot != null)
        {
            standCameraHeight = cameraPivot.localPosition.y;
            standCameraForward = cameraPivot.localPosition.z;
            cameraBase = cameraPivot.localPosition;
        }
        IsThirdPerson = startInThirdPerson;
        if (bodyVisual == null)
            bodyVisual = transform.Find("Body");

        if (disableOtherMainCameras)
        {
            Camera myCamera = cameraPivot != null ? cameraPivot.GetComponentInChildren<Camera>() : null;
            foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (cam != myCamera && cam.CompareTag("MainCamera"))
                    cam.gameObject.SetActive(false);
            }
        }

        if (snapToFloorOnStart)
            SnapToFloor();

        spawnPoint = transform.position;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        bool paused = Time.timeScale == 0f;

        if (!paused && Cursor.lockState == CursorLockMode.Locked)
            Look();

        if (!paused && Input.GetKeyDown(switchViewKey))
            IsThirdPerson = !IsThirdPerson;

        UpdateCrouch(paused);
        Move(paused);

        if (transform.position.y < fallResetHeight)
            Teleport(spawnPoint);
    }

    private void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(0f, mouseX, 0f);

        pitch = Mathf.Clamp(pitch - mouseY, -maxLookAngle, maxLookAngle);
        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void Move(bool paused)
    {
        float h = paused ? 0f : Input.GetAxisRaw("Horizontal");
        float v = paused ? 0f : Input.GetAxisRaw("Vertical");

        Vector3 input = transform.right * h + transform.forward * v;
        input = Vector3.ClampMagnitude(input, 1f);
        IsMoving = input.sqrMagnitude > 0.01f;

        bool wantsSprint = !paused && IsMoving && !IsCrouching && Input.GetKey(KeyCode.LeftShift);

        float speed;
        if (IsCrouching)
        {
            if (stamina != null) stamina.SetSprinting(false);
            speed = crouchSpeed;
            IsSprinting = false;
        }
        else if (stamina != null)
        {
            stamina.SetSprinting(wantsSprint);
            speed = stamina.GetMovementSpeed();
            IsSprinting = wantsSprint && stamina.CanSprint;
        }
        else
        {
            speed = wantsSprint ? sprintSpeed : walkSpeed;
            IsSprinting = wantsSprint;
        }

        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, input * speed, acceleration * Time.deltaTime);

        if (controller.isGrounded)
        {
            lastGroundedTime = Time.time;
            if (verticalVelocity < 0f)
                verticalVelocity = -2f;
        }

        bool canJump = Time.time - lastGroundedTime <= coyoteTime;
        if (!paused && canJump && !IsCrouching && Input.GetButtonDown("Jump"))
        {
           
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastGroundedTime = -10f;
        }

        verticalVelocity += gravity * Time.deltaTime;

        CollisionFlags flags = controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);

        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;
    }

    private void UpdateCrouch(bool paused)
    {
        if (!paused)
        {
            bool pressed = Input.GetKeyDown(crouchKey) || Input.GetKeyDown(altCrouchKey);
            if (pressed)
                crouchToggled = !crouchToggled;
        }

        bool wantsCrouch = holdToCrouch
            ? !paused && (Input.GetKey(crouchKey) || Input.GetKey(altCrouchKey))
            : crouchToggled;

        if (!wantsCrouch && IsCrouching && !HasRoomToStand())
            wantsCrouch = true;
        if (!holdToCrouch && !wantsCrouch)
            crouchToggled = false;

        IsCrouching = wantsCrouch;

        float target = IsCrouching ? 1f : 0f;
        crouchAmount = Mathf.MoveTowards(crouchAmount, target, crouchTransitionSpeed * Time.deltaTime);

        float height = Mathf.Lerp(standHeight, crouchHeight, crouchAmount);
        controller.height = height;
        controller.center = new Vector3(0f, height / 2f, 0f);

        cameraBase = new Vector3(cameraBase.x,
            Mathf.Lerp(standCameraHeight, crouchCameraHeight, crouchAmount),
            Mathf.Lerp(standCameraForward, crouchCameraForward, crouchAmount));

        if (bodyVisual != null)
            bodyVisual.localScale = new Vector3(1f, height / standHeight, 1f);
    }

    private void LateUpdate()
    {
        if (cameraPivot == null)
            return;

        if (!IsThirdPerson)
        {
            cameraPivot.localPosition = cameraBase;
            return;
        }

        Vector3 desiredLocal = cameraBase + cameraPivot.localRotation * thirdPersonOffset;
        Vector3 from = transform.TransformPoint(cameraBase);
        Vector3 to = transform.TransformPoint(desiredLocal);
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        int mask = ~(1 << gameObject.layer);
        if (dist > 0.001f && Physics.SphereCast(from, cameraCollisionRadius, dir / dist, out RaycastHit hit, dist, mask, QueryTriggerInteraction.Ignore))
            to = from + dir / dist * Mathf.Max(0.05f, hit.distance);
        cameraPivot.localPosition = transform.InverseTransformPoint(to);
    }

    private bool HasRoomToStand()
    {
        float radius = controller.radius * 0.95f;
        Vector3 bottom = transform.position + Vector3.up * (controller.height - radius);
        Vector3 top = transform.position + Vector3.up * (standHeight - radius + 0.02f);
        if (top.y <= bottom.y) return true;
        int mask = ~(1 << gameObject.layer);
        return !Physics.CheckCapsule(bottom, top, radius, mask, QueryTriggerInteraction.Ignore);
    }

    private void SnapToFloor()
    {
        Physics.SyncTransforms();
        int mask = ~(1 << gameObject.layer);

        if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out RaycastHit hit, 50f, mask, QueryTriggerInteraction.Ignore))
        {
            Teleport(hit.point + Vector3.up * 0.05f);
            return;
        }

        if (string.IsNullOrEmpty(fallbackFloorName))
            return;

        Renderer best = null;
        Vector3 centre = Vector3.zero;
        int count = 0;
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (Renderer r in renderers)
        {
            if (r.name.Contains(fallbackFloorName)) { centre += r.bounds.center; count++; }
        }
        if (count == 0)
        {
            Debug.LogWarning($"{name}: no floor below the spawn point and no '{fallbackFloorName}' objects found. Move the child above a floor.", this);
            return;
        }
        centre /= count;
        float bestDist = float.MaxValue;
        foreach (Renderer r in renderers)
        {
            if (!r.name.Contains(fallbackFloorName)) continue;
            float d = (r.bounds.center - centre).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = r; }
        }

        Bounds b = best.bounds;
        Vector3 rayStart = new Vector3(b.center.x, b.max.y + 0.5f, b.center.z);
        if (Physics.Raycast(rayStart, Vector3.down, out hit, 5f, mask, QueryTriggerInteraction.Ignore))
            Teleport(hit.point + Vector3.up * 0.05f);
        else
            Teleport(new Vector3(b.center.x, b.max.y + 0.05f, b.center.z));

        Debug.Log($"{name}: nothing under the start point, spawned on '{best.name}' instead. Move the ChildPlayer in the scene to choose a spawn spot.", this);
    }

    private void Teleport(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        controller.enabled = true;
    }
}
