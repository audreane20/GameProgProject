using UnityEngine;

public class FlickeringLight : MonoBehaviour
{
    public Light targetLight;
    public float minimumIntensity = 0f;
    public float maximumIntensity = 2f;
    public float minimumDelay = 0.05f;
    public float maximumDelay = 0.3f;

    private float timer;

    void Update()
    {
        if (targetLight == null)
            return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            targetLight.intensity = Random.Range(
                minimumIntensity,
                maximumIntensity
            );

            timer = Random.Range(minimumDelay, maximumDelay);
        }
    }
}