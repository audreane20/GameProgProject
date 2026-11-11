using UnityEngine;

//uses scorePickup.cs
// Needs a collider with Is Trigger ticked on the same GameObject
// Attach to NoteInteraction, not NotePanel
public class ReadableNote : MonoBehaviour
{
    [Header("Note Interface")]
    [SerializeField] private GameObject readPrompt;
    [SerializeField] private GameObject notePanel;

    private bool playerNearby;
    private bool noteOpen;

    // Remember the mouse settings before opening the note
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;

    private void Start()
    {
        // Hide the instructions and note at the beginning.
        readPrompt.SetActive(false);
        notePanel.SetActive(false);
    }

    // Called automatically when something enters this trigger
    private void OnTriggerEnter(Collider other)
    {
        // Only react to the player, ignore everything else
        if (other.CompareTag("Player"))
        {
            // show a prompt instead of adding points like lab
            playerNearby = true;
            readPrompt.SetActive(!noteOpen);

            // We do not destroy the note like the original coin
            // The player can read it again.
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Use the same player check when leaving the trigger
        if (other.CompareTag("Player"))
        {
            playerNearby = false;

            if (noteOpen)
            {
                CloseNote();
            }

            readPrompt.SetActive(false);
        }
    }

    private void Update()
    {
        // Do not interact while the pause menu stops game time
        if (Time.timeScale == 0f)
        {
            return;
        }

        // Added for our game: press E near the note
        if (playerNearby && Input.GetKeyDown(KeyCode.E))
        {
            if (noteOpen)
            {
                CloseNote();
            }
            else
            {
                OpenNote();
            }
        }
    }

    private void OpenNote()
    {
        // Save the mouse settings before changing them
        previousCursorVisible = Cursor.visible;
        previousCursorLock = Cursor.lockState;

        noteOpen = true;

        // Replace the nearby prompt with the note panel
        readPrompt.SetActive(false);
        notePanel.SetActive(true);

        // Allow clicking the Close button
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Public so the close button can call it, like the button methods in the class SceneLoader script
    public void CloseNote()
    {
        // Ignore extra close requests if the note is already closed
        if (!noteOpen)
        {
            return;
        }

        noteOpen = false;
        notePanel.SetActive(false);

        // Show the prompt again only if still near the note
        readPrompt.SetActive(playerNearby);

        // Restore the previous mouse settings
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
    }
}