using UnityEngine;

// Sends the child's movement state to the kid model's Animator.
// Animator parameters: Speed (float), Crouch (bool), Grounded (bool).
[RequireComponent(typeof(ChildPlayerController))]
public class ChildPlayerAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [Tooltip("Smooths speed changes so the blend between idle, walk and run doesn't snap.")]
    [SerializeField] private float speedDampTime = 0.1f;
    [Tooltip("Mesh hidden from the camera in first person so it doesn't block the view (still casts a shadow).")]
    [SerializeField] private string headMeshName = "KidHead";

    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int CrouchId = Animator.StringToHash("Crouch");
    private static readonly int GroundedId = Animator.StringToHash("Grounded");

    private ChildPlayerController player;
    private CharacterController controller;
    private Renderer[] headRenderers = new Renderer[0];
    private bool lastThirdPerson;

    private void Start()
    {
        player = GetComponent<ChildPlayerController>();
        controller = GetComponent<CharacterController>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
        {
            var found = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer r in animator.GetComponentsInChildren<Renderer>(true))
                if (r.name == headMeshName) found.Add(r);
            headRenderers = found.ToArray();
        }

        lastThirdPerson = player.IsThirdPerson;
        UpdateHeadVisibility();
    }

    private void Update()
    {
        if (animator == null)
            return;

        Vector3 v = controller.velocity;
        v.y = 0f;
        animator.SetFloat(SpeedId, v.magnitude, speedDampTime, Time.deltaTime);
        animator.SetBool(CrouchId, player.IsCrouching);
        animator.SetBool(GroundedId, player.IsGroundedStable);

        if (player.IsThirdPerson != lastThirdPerson)
        {
            lastThirdPerson = player.IsThirdPerson;
            UpdateHeadVisibility();
        }
    }

    private void UpdateHeadVisibility()
    {
        var mode = player.IsThirdPerson
            ? UnityEngine.Rendering.ShadowCastingMode.On
            : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        foreach (Renderer r in headRenderers)
            r.shadowCastingMode = mode;
    }
}
