using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverController : MonoBehaviour
{
    public void RestartGame()
    {
        SceneManager.LoadScene("SampleScene");
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