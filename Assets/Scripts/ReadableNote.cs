using UnityEngine;

// Shows a prompt near the table and opens the note with E button. Closes the note with E or the Close button.
public class ReadableNote : MonoBehaviour
{
    [Header("Note Interface")]
    [SerializeField] private GameObject readPrompt;
    [SerializeField] private GameObject notePanel;

    private bool playerNearby;
    private bool noteOpen;

    // Store the cursor settings so closing restores them
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;

    private void Start()
    {
        // no message should appear before approaching the note
        readPrompt.SetActive(false);
        notePanel.SetActive(false);
    }

    private void Update()
    {
        // Do not open a note while the pause menu is active
        if (Time.timeScale == 0f)
        {
            return;
        }

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

    private void OnTriggerEnter(Collider other)
    {
        // Ignore objects that are not tagged Player
        if (!other.CompareTag("Player"))
        {
            return;
        }

        playerNearby = true;
        readPrompt.SetActive(!noteOpen);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        playerNearby = false;

        // Close the note if the player leaves its interaction zone
        if (noteOpen)
        {
            CloseNote();
        }

        readPrompt.SetActive(false);
    }

    private void OpenNote()
    {
        previousCursorVisible = Cursor.visible;
        previousCursorLock = Cursor.lockState;

        noteOpen = true;
        readPrompt.SetActive(false);
        notePanel.SetActive(true);

        // Let the player click the Close button.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Public so CloseNoteButton can call it.
    public void CloseNote()
    {
        noteOpen = false;
        notePanel.SetActive(false);
        readPrompt.SetActive(playerNearby);

        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
    }
}
