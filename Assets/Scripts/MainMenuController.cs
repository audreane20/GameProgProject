using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    public GameObject instructionsPanel;
    public GameObject playButton;
    public GameObject instructionsButton;
    public GameObject quitButton;
    public GameObject TitleText;

    public void PlayGame()
    {
        SceneManager.LoadScene("SampleScene");
    }

    public void QuitGame()
    {
        Debug.Log("Quit button clicked.");
        Application.Quit();
    }

    public void OpenInstructions()
    {
        playButton.SetActive(false);
        instructionsButton.SetActive(false);
        quitButton.SetActive(false);
        TitleText.SetActive(false);

        instructionsPanel.SetActive(true);
    }

    public void CloseInstructions()
    {
        instructionsPanel.SetActive(false);

        playButton.SetActive(true);
        instructionsButton.SetActive(true);
        quitButton.SetActive(true);
        TitleText.SetActive(true);
    }

}