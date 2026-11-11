using UnityEngine;

// TEMPORARY: tests stamina before the real movement script is ready.
// Holding Left Shift sprints
// Remove this when integrating real player movement
public class StaminaTestInput : MonoBehaviour
{
    private PlayerStamina stamina;

    private void Start()
    {
        stamina = GetComponent<PlayerStamina>();
    }

    private void Update()
    {
        // Only request sprinting while game time is running(left shift is pressed and game is not paused)
        bool sprintRequested = Input.GetKey(KeyCode.LeftShift) && Time.timeScale > 0f;
        stamina.SetSprinting(sprintRequested);
    }
}
