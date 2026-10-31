using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class EndingController : MonoBehaviour
{
    public Image blackOverlay;
    public AudioSource endingAudio;
    public RectTransform endingContent;

    public float fadeDuration = 8f;
    public float scrollDelay = 12f;
    public float scrollSpeed = 80f;

    private float elapsedTime;

    void Start()
    {
        elapsedTime = 0f;

        // Begin with the content below the screen.
        endingContent.anchoredPosition = new Vector2(0f, -1900f);
        endingContent.gameObject.SetActive(false);

        SetBlackOpacity(0f);
        endingAudio.Play();
    }

    void Update()
    {
        elapsedTime += Time.deltaTime;

        float fadeProgress = Mathf.Clamp01(
            elapsedTime / Mathf.Max(fadeDuration, 0.01f)
        );

        SetBlackOpacity(fadeProgress);

        // Wait for both the blackout and the chosen audio moment.
        if (elapsedTime < Mathf.Max(fadeDuration, scrollDelay))
            return;

        endingContent.gameObject.SetActive(true);

        endingContent.anchoredPosition = Vector2.MoveTowards(
            endingContent.anchoredPosition,
            Vector2.zero,
            scrollSpeed * Time.deltaTime
        );
    }

    void SetBlackOpacity(float opacity)
    {
        Color colour = blackOverlay.color;
        colour.a = opacity;
        blackOverlay.color = colour;
    }

    public void ReturnToMenu()
    {
        SceneManager.LoadScene("Main menu");
    }

    public void QuitGame()
    {
        Debug.Log("Quit button clicked.");
        Application.Quit();
    }
}