using System.Collections;
using UnityEngine;

public class KeepButtonAudioAlive : MonoBehaviour
{
    public void PlayClick()
    {
        AudioSource original = GetComponent<AudioSource>();

        if (original == null || original.clip == null)
            return;

        GameObject soundObject = new GameObject("ButtonClick");
        DontDestroyOnLoad(soundObject);

        AudioSource sound = soundObject.AddComponent<AudioSource>();
        sound.clip = original.clip;
        sound.volume = original.volume;
        sound.spatialBlend = 0f;
        sound.loop = false;
        sound.Play();

        soundObject.AddComponent<TemporaryButtonSound>();
    }
}

public class TemporaryButtonSound : MonoBehaviour
{
    IEnumerator Start()
    {
        AudioSource sound = GetComponent<AudioSource>();

        yield return new WaitForSecondsRealtime(sound.clip.length);

        Destroy(gameObject);
    }
}