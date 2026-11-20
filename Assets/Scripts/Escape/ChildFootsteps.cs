using UnityEngine;

// Plays footstep sounds while the kid walks. Quieter when crouching, faster when running.
[RequireComponent(typeof(ChildPlayerController))]
public class ChildFootsteps : MonoBehaviour
{
    public AudioClip clip;
    public float walkInterval = 0.42f;
    public float runInterval = 0.28f;
    public float crouchInterval = 0.65f;
    [Range(0f, 1f)] public float volume = 0.5f;

    private ChildPlayerController player;
    private AudioSource source;
    private float timer;

    private void Start()
    {
        player = GetComponent<ChildPlayerController>();
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    private void Update()
    {
        if (clip == null || !player.IsMoving || !player.IsGroundedStable || Time.timeScale == 0f)
        {
            timer = 0.1f;
            return;
        }

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer = player.IsCrouching ? crouchInterval : player.IsSprinting ? runInterval : walkInterval;
        source.pitch = Random.Range(0.9f, 1.15f);
        source.PlayOneShot(clip, player.IsCrouching ? volume * 0.35f : volume);
    }
}
