using UnityEngine;
using UnityEngine.UI; //for Slider and Image

// stamina for hospital games sprint mechanic
// The movement script tells this script when sprinting starts or stops
public class PlayerStamina : MonoBehaviour
{
    [Header("Stamina Settings")]

    // Maximum amount of stamina the player can have
    [SerializeField] private float maxStamina = 100f;
    // Stamina used each second while sprinting
    [SerializeField] private float drainRate = 25f;
    // Stamina restored each second while resting
    [SerializeField] private float recoveryRate = 15f;
    // After exhaustion, recover this before sprinting again
    [SerializeField] private float restartStamina = 25f;

    [Header("Movement Speed")]

    // Normal walking speed for our hospital game
    [SerializeField] private float movementSpeed = 3f;

    // Multiplies walking speed while sprinting
    [SerializeField] private float sprintMultiplier = 2f;

    [Header("Stamina References")]
    [SerializeField] private Slider staminaBar;
    [SerializeField] private Image staminaFill;

    [Header("Get Colors")]
    [SerializeField] private Color normalColor = Color.green;
    [SerializeField] private Color lowColor = Color.red;
    // GameObject is enough because we only show or hide the text:
    [SerializeField] private GameObject exhaustedText;

    private float currentStamina;
    private bool isSprinting;
    private bool isExhausted;

    // other scripts can read these values but cannot change them:
    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public bool CanSprint => !isExhausted && currentStamina > 0f;
    // Lets the HUD read if the player is tired
    // Other scripts cannot change isExhausted through this property.
    public bool IsExhausted => isExhausted;

    private void Start()
    {
        // start level with a full stamina bar
        currentStamina = maxStamina;

        // Match the slider's range to the player's stamina settings
        staminaBar.minValue = 0f;
        staminaBar.maxValue = maxStamina;

        UpdateHUD();
    }

    private void Update()
    {
        if (isSprinting && CanSprint)
        {
            // Multiply by deltaTime so the drain is per second not computer's frame rate
            currentStamina -= drainRate * Time.deltaTime;

            if (currentStamina <= 0f)
            {
                currentStamina = 0f;
                isExhausted = true;
                isSprinting = false;
            }
        }
        else
        {
            // Recover stamina when the player is not sprinting
            currentStamina += recoveryRate * Time.deltaTime;
        }

        // Keep stamina between zero and max
        //doesnt allow stamina to go below emptu of more than full
        //Mathf.Clamp -> built-in Unity function that keeps a number in a range, Mathf = Unity’s collection of maths functions and Clamp = limits a number between a minimum and maximum, if above 100(max) it will return 100, if below 0(min) it will return 0, if between 0 and 100 it will return the actual number
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);

        // not sprinting right away again after recovering do only a small amount of stamina
        if (isExhausted && currentStamina >= restartStamina)
        {
            isExhausted = false;
        }

        UpdateHUD();
    }

    private void UpdateHUD()
    {
        // Read the gameplay value and display it on the slider
        staminaBar.value = currentStamina;

        // Calculate 25 percent of maximum stamina
        float lowStamina = maxStamina * 0.25f;

        // Color feedback makes low stamina easy to visualize
        if (currentStamina <= lowStamina)
        {
            staminaFill.color = lowColor;
        }
        else
        {
            staminaFill.color = normalColor;
        }

        // Show the warning when exhausted
        // Hide it when enough stamina has recovered
        exhaustedText.SetActive(isExhausted);
    }

    // The movement script calls this every frame
    // Pass true only when the player is actually sprinting
    public void SetSprinting(bool sprinting) //PLAYER HAS TO CALL THIS FUNCTION EVERY FRAME, PASSING TRUE WHEN SPRINTING AND FALSE WHEN NOT SPRINTING
    {
        isSprinting = sprinting && CanSprint;
    }

    // Our version checks stamina before allowing boosted speed
    public float GetMovementSpeed()
    {
        // Boost only while sprinting with stamina available
        bool isBoosting = isSprinting && CanSprint;

        // When boosting, multiply walking speed by sprintMultiplier or multiply by 1 to keep normal walking speed
        float speed = movementSpeed * (isBoosting ? sprintMultiplier : 1f);

        return speed;
    }
}