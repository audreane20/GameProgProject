using UnityEngine;

public class HeartbeatController : MonoBehaviour
{
    public AudioSource heartbeatSource;
    public PlayerStamina playerStamina;

    public float restingPitch = 0.8f;
    public float exhaustedPitch = 1.5f;

    [Range(0f, 1f)]
    public float restingVolume = 0.15f;

    [Range(0f, 1f)]
    public float exhaustedVolume = 0.6f;

    public float changeSpeed = 2f;

    void Update()
    {
        if (heartbeatSource == null || playerStamina == null)
            return;

        float tiredness = 1f - Mathf.Clamp01(
            playerStamina.CurrentStamina /
            Mathf.Max(playerStamina.MaxStamina, 0.01f)
        );

        float targetPitch = Mathf.Lerp(
            restingPitch, exhaustedPitch, tiredness
        );

        float targetVolume = Mathf.Lerp(
            restingVolume, exhaustedVolume, tiredness
        );

        heartbeatSource.pitch = Mathf.MoveTowards(
            heartbeatSource.pitch,
            targetPitch,
            changeSpeed * Time.deltaTime
        );

        heartbeatSource.volume = Mathf.MoveTowards(
            heartbeatSource.volume,
            targetVolume,
            changeSpeed * Time.deltaTime
        );
    }
}