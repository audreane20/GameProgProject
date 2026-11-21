using System.Collections;

using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public class EscapeGameManager : MonoBehaviour
{
    public static EscapeGameManager Instance { get; private set; }
    [Header("Key")]
    [SerializeField] private KeyPickup key;
    [SerializeField] private Transform[] keySpawnPoints;

    [Header("Ending")]
    [SerializeField] private string escapeSceneName = "EndingScene";
    [SerializeField] private float fadeOutTime = 2.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip ambience;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.35f;
    [SerializeField] private AudioClip keyPickupSound;
    [SerializeField] private AudioClip doorLockedSound;
    [SerializeField] private AudioClip doorOpenSound;
    [SerializeField] private AudioClip footstepSound;
    [SerializeField] private AudioClip[] scarySounds;
    [SerializeField] private Vector2 scarySoundInterval = new Vector2(18f, 40f);

    [Header("Player")]
    [SerializeField] private bool givePlayerFlashlight = true;

    [Header("Caught by the monster")]
    [SerializeField] private string[] caughtQuotes =
    {
        "You can't stop now...",
        "It saw you move.",
        "Don't. Move.",
        "It's still hungry...",
        "When its eyes turn red... freeze.",
    };
    [SerializeField] private float quoteTime = 3.5f;

    public bool HasKey { get; private set; }
    public Transform Player { get; private set; }
    public bool IsOver => escaping || caught;

    private TextMeshProUGUI objectiveText, promptText, messageText;
    private Image fadeImage;
    private int promptFrame = -1;
    private Coroutine messageRoutine;
    private AudioSource sfx;
    private bool escaping;
    private bool caught;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        Player = p != null ? p.transform : null;

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;

        BuildUI();
        SetUpPlayer();
        PlaceKey();
        StartAmbience();
        if (scarySounds != null && scarySounds.Length > 0)
            StartCoroutine(ScarySoundLoop());

        SetObjective("Find the key to the front door");
        StartCoroutine(Intro());
    }

    private void LateUpdate()
    {
<<<<<<< Updated upstream
=======
        if (messageText != null)
            messageText.enabled = Time.timeScale > 0f;

        // Prompts only stay up while something keeps asking for them.
>>>>>>> Stashed changes
        if (promptText != null && promptFrame < Time.frameCount)
            promptText.text = "";
    }


    public void ShowPrompt(string text)
    {
        if (IsOver || promptText == null) return;
        promptText.text = text;
        promptFrame = Time.frameCount;
    }

    public void OnKeyPickedUp()
    {
        HasKey = true;
        PlaySound(keyPickupSound, 0.9f);
        ShowMessage("You found the key!", 3f);
        SetObjective("Escape through the front door");
    }

    public void OnDoorLocked()
    {
        PlaySound(doorLockedSound, 0.7f);
        ShowMessage("It's locked. There must be a key somewhere...", 3f);
    }

    public void OnExitOpened()
    {
        if (IsOver) return;
        escaping = true;
        PlaySound(doorOpenSound, 1f);
        SetObjective("");
        StartCoroutine(EscapeSequence());
    }

    private void SetUpPlayer()
    {
        Camera cam = Player != null ? Player.GetComponentInChildren<Camera>() : Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;

            if (givePlayerFlashlight && cam.GetComponent<Flashlight>() == null)
                cam.gameObject.AddComponent<Flashlight>();
        }

        if (Player != null && footstepSound != null)
        {
            var steps = Player.GetComponent<ChildFootsteps>();
            if (steps == null) steps = Player.gameObject.AddComponent<ChildFootsteps>();
            steps.clip = footstepSound;
        }
    }

    private void PlaceKey()
    {
        if (key == null || keySpawnPoints == null || keySpawnPoints.Length == 0) return;
        Transform spot = keySpawnPoints[Random.Range(0, keySpawnPoints.Length)];
        key.transform.SetPositionAndRotation(spot.position, spot.rotation);
    }

    private void StartAmbience()
    {
        if (ambience == null) return;
        var a = gameObject.AddComponent<AudioSource>();
        a.clip = ambience;
        a.loop = true;
        a.volume = ambienceVolume;
        a.spatialBlend = 0f;
        a.Play();
    }

    private IEnumerator ScarySoundLoop()
    {
        yield return new WaitForSeconds(Random.Range(8f, 14f));
        while (!IsOver)
        {
            if (Player != null)
            {
                AudioClip clip = scarySounds[Random.Range(0, scarySounds.Length)];
                Vector2 dir = Random.insideUnitCircle.normalized;
                // mostly behind the player
                Vector3 behind = -Player.forward * 0.7f + new Vector3(dir.x, 0f, dir.y) * 0.5f;
                Vector3 pos = Player.position + behind.normalized * Random.Range(5f, 9f) + Vector3.up * 1.2f;
                if (clip != null) AudioSource.PlayClipAtPoint(clip, pos, 0.9f);
            }
            yield return new WaitForSeconds(Random.Range(scarySoundInterval.x, scarySoundInterval.y));
        }
    }

    private IEnumerator Intro()
    {
        SetFade(1f);
        yield return new WaitForSeconds(0.5f);
        ShowMessage("You wake up alone in the hospital. It's dark... and something is wrong.", 5f);
        for (float t = 0f; t < 3f; t += Time.deltaTime)
        {
            SetFade(1f - t / 3f);
            yield return null;
        }
        SetFade(0f);
        yield return new WaitForSeconds(2.5f);
        ShowMessage("F toggles your flashlight.  Find the key and get out.", 4f);
        yield return new WaitForSeconds(6f);
        if (!IsOver)
            ShowMessage("Something walks these halls.  If its eyes turn red... don't move.", 5f);
    }

    public void OnPlayerCaught(MonsterAI monster)
    {
        if (IsOver) return;
        caught = true;
        StartCoroutine(CaughtSequence(monster));
    }

    private IEnumerator CaughtSequence(MonsterAI monster)
    {
        SetObjective("");
        if (promptText != null) promptText.text = "";
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        if (messageText != null) messageText.text = "";

        if (Player != null)
        {
            var move = Player.GetComponent<ChildPlayerController>();
            if (move != null) move.enabled = false;
            var steps = Player.GetComponent<ChildFootsteps>();
            if (steps != null) steps.enabled = false;
        }

        Camera cam = Player != null ? Player.GetComponentInChildren<Camera>() : Camera.main;
        if (cam != null && monster != null)
        {
            Transform ct = cam.transform;
            ct.SetParent(null, true);
            Vector3 face = monster.FacePoint.position;
            Vector3 away = ct.position - face;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = monster.transform.forward;
            Vector3 target = face + away.normalized * 0.7f - Vector3.up * 0.05f;
            Vector3 start = ct.position;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.12f)
            {
                ct.position = Vector3.Lerp(start, target, t);
                ct.rotation = Quaternion.LookRotation(face - ct.position);
                yield return null;
            }
            for (float t = 0f; t < 1.1f; t += Time.deltaTime)
            {
                ct.position = target + Random.insideUnitSphere * 0.03f;
                ct.rotation = Quaternion.LookRotation(monster.FacePoint.position - ct.position);
                if (fadeImage != null)
                    fadeImage.color = new Color(0.5f, 0f, 0f, Random.value < 0.5f ? 0.45f : 0f);
                yield return null;
            }
        }
        else yield return new WaitForSeconds(1.2f);

        SetFade(1f);
        if (messageText != null && caughtQuotes.Length > 0)
        {
            messageText.color = new Color(0.75f, 0.05f, 0.05f, 1f);
            messageText.fontSize = 64;
            messageText.alpha = 1f;
            messageText.text = caughtQuotes[Random.Range(0, caughtQuotes.Length)];
        }
        yield return new WaitForSeconds(quoteTime);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private IEnumerator EscapeSequence()
    {
        ShowMessage("You escaped...", fadeOutTime + 2f);
        yield return new WaitForSeconds(0.8f);
        for (float t = 0f; t < fadeOutTime; t += Time.deltaTime)
        {
            SetFade(t / fadeOutTime);
            yield return null;
        }
        SetFade(1f);
        yield return new WaitForSeconds(1f);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (Application.CanStreamedLevelBeLoaded(escapeSceneName))
            SceneManager.LoadScene(escapeSceneName);
        else
            Debug.LogWarning($"EscapeGameManager: scene '{escapeSceneName}' isn't in the build settings.");
    }

    private void BuildUI()
    {
        var canvasGO = new GameObject("EscapeUI", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        objectiveText = MakeText(canvas.transform, "Objective", new Vector2(0, 1), new Vector2(40, -100), 34, TextAlignmentOptions.TopLeft, new Vector2(900, 80));
        objectiveText.color = new Color(0.85f, 0.85f, 0.8f, 0.9f);
        promptText = MakeText(canvas.transform, "Prompt", new Vector2(0.5f, 0), new Vector2(0, 160), 36, TextAlignmentOptions.Center, new Vector2(1400, 80));
        messageText = MakeText(canvas.transform, "Message", new Vector2(0.5f, 0.5f), new Vector2(0, -180), 40, TextAlignmentOptions.Center, new Vector2(1600, 140));
        messageText.color = new Color(0.9f, 0.85f, 0.8f, 1f);

        var fadeGO = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        fadeGO.transform.SetParent(canvas.transform, false);
        var rt = fadeGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        fadeImage = fadeGO.GetComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;
        fadeGO.transform.SetAsFirstSibling();
    }

    private static TextMeshProUGUI MakeText(Transform parent, string name, Vector2 anchor, Vector2 pos, float size, TextAlignmentOptions align, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.alignment = align;
        t.raycastTarget = false;
        t.text = "";
        return t;
    }

    private void SetObjective(string text)
    {
        if (objectiveText != null)
            objectiveText.text = string.IsNullOrEmpty(text) ? "" : "Objective: " + text;
    }

    private void ShowMessage(string text, float seconds)
    {
        if (messageText == null) return;
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(MessageRoutine(text, seconds));
    }

    private IEnumerator MessageRoutine(string text, float seconds)
    {
        messageText.text = text;
        messageText.alpha = 1f;
        yield return new WaitForSeconds(seconds);
        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            messageText.alpha = 1f - t;
            yield return null;
        }
        messageText.text = "";
    }

    private void SetFade(float a)
    {
        if (fadeImage != null)
            fadeImage.color = new Color(0f, 0f, 0f, a);
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip != null) sfx.PlayOneShot(clip, volume);
    }
}
