using System.Collections;
using UnityEngine;

// A flashlight on the player's camera. F turns it on and off.
// It flickers now and then, just to keep you nervous.
public class Flashlight : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.F;
    public bool startOn = true;
    public float intensity = 11f;
    public float range = 30f;
    public float spotAngle = 70f;
    public Color colour = new Color(1f, 0.93f, 0.8f);
    public Vector2 flickerEvery = new Vector2(15f, 35f);

    private Light beam;
    private bool on;

    public bool IsOn => on;

    private void Awake()
    {
        var go = new GameObject("FlashlightBeam");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0.12f, -0.12f, 0.1f);
        beam = go.AddComponent<Light>();
        beam.type = LightType.Spot;
        beam.color = colour;
        beam.intensity = intensity;
        beam.range = range;
        beam.spotAngle = spotAngle;
        beam.innerSpotAngle = spotAngle * 0.5f;
        beam.shadows = LightShadows.Soft;
        on = startOn;
        beam.enabled = on;
        StartCoroutine(RandomFlicker());
    }

    private void Update()
    {
        if (Time.timeScale > 0f && Input.GetKeyDown(toggleKey))
        {
            on = !on;
            beam.enabled = on;
        }
    }

    private IEnumerator RandomFlicker()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(flickerEvery.x, flickerEvery.y));
            if (!on) continue;
            int flickers = Random.Range(3, 7);
            for (int i = 0; i < flickers; i++)
            {
                beam.intensity = intensity * Random.Range(0f, 0.4f);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
                beam.intensity = intensity;
                yield return new WaitForSeconds(Random.Range(0.03f, 0.15f));
            }
        }
    }
}
