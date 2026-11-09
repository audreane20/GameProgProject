using UnityEngine;
using UnityEngine.SceneManagement; // Allows us to load another scene
//Escape opens and closes the pause menu.
//Resume closes the pause menu.
//mouse becomes available while paused.

public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel;
    // The panel with the controls instructions and Back button
    public GameObject controlsPanel;
    // Main pause-screen objects to hide while viewing controls.
    public GameObject pauseTitle;
    public GameObject resumeButton;
    public GameObject controlsButton;
    public GameObject mainMenuButton;

    void Start()
    {
        // hides menu when gameplay starts
        pausePanel.SetActive(false);
        // Keep the instructions hidden when scene begins
        controlsPanel.SetActive(false);
    }

    private void Update()
    {

        // GetKeyDown checks initial press not holding the key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePauseMenu();
        }
    }

    // Public lets a UI Button call this from On Click()
    public void ResumeGame()
    {
        controlsPanel.SetActive(false);
        pausePanel.SetActive(false);

        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }


    public void TogglePauseMenu()
    {
        // activeSelf tells whether the panel is currently active
        // ! = not so passing opposite of current state
        // if active pass false to hide it;
        // if inactive pass true to show it.
        SetPauseMenuActivation(!pausePanel.activeSelf);
    }

   
    void SetPauseMenuActivation(bool active)
    {
        // Reset the instructions to the main pause buttons
        CloseControls();

        // Show or hide the whole pause menu
        pausePanel.SetActive(active);

        if (active)
        {
            // Stop game time while the pause menu is open
            Time.timeScale = 0f;

            // Release and show the mouse for clicking buttons
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // Continue gameplay.
            Time.timeScale = 1f;

            // Return the mouse to first-person gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
        

    // don't leave game time paused when this script is disabled or its HUD is destroyed during a scene change
    private void OnDisable()
    {
        Time.timeScale = 1f;
    }

    public void ReturnToMainMenu()
    {
    // Restore normal game speed before leaving the paused scene
    Time.timeScale = 1f;

    // Release the mouse so the player can click the menu buttons
    Cursor.lockState = CursorLockMode.None;
    Cursor.visible = true;

    // Load our existing menu scene.
    SceneManager.LoadScene("Main menu");
    }   

    // Show only the controls instructions and Back button
    public void OpenControls()
    {
        // Hide the normal pause-screen information
        pauseTitle.SetActive(false);
        resumeButton.SetActive(false);
        controlsButton.SetActive(false);
        mainMenuButton.SetActive(false);
        // Show the instructions and Back button
        controlsPanel.SetActive(true);
    }

    // Return to the normal pause screen without resuming gameplay
    public void CloseControls()
    {
        controlsPanel.SetActive(false);
        pauseTitle.SetActive(true);
        resumeButton.SetActive(true);
        controlsButton.SetActive(true);
        mainMenuButton.SetActive(true);
    }
}