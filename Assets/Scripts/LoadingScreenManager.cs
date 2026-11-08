using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager Instance;
    public GameObject m_LoadingScreenObject;
    public Slider ProgressBar;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
        } else {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }        
    }

    public void SwitchToScene(int id) {
        m_LoadingScreenObject.SetActive(true);
        ProgressBar.value = 0;
        StartCoroutine(SwitchToSceneAsync(id));
    }

    IEnumerator SwitchToSceneAsync(int id)
    {
        // Start loading the scene asynchronously
        AsyncOperation operation = SceneManager.LoadSceneAsync(id);
        while (!operation.isDone) // While the scene is still loading
        {
            // Update the progress bar based on the loading progress
            ProgressBar.value = operation.progress;
            yield return null;
        }
        // Wait a short time to ensure the loading screen is visible for a moment
        yield return new WaitForSeconds(0.2f);
        m_LoadingScreenObject.SetActive(false);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
