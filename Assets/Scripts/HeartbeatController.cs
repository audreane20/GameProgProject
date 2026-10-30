using UnityEngine;

public class HeartbeatController : MonoBehaviour
{
    public AudioSource heartbeatSource;
    public float targetPitch = 1.5f;
    public float pitchChangeSpeed = 0.1f;
    public float startDelay = 3f;

    private float elapsedTime;

    void Update()
    {
        elapsedTime += Time.deltaTime;

        if (elapsedTime < startDelay || heartbeatSource == null)
            return;

        heartbeatSource.pitch = Mathf.MoveTowards(
            heartbeatSource.pitch,
            targetPitch,
            pitchChangeSpeed * Time.deltaTime
        );
    }
}