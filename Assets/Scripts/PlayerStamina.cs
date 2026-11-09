using UnityEngine;

// Stores stamina for hospital games sprint mechanic
// The movement script tells this script when sprinting starts or stops
// The HUD reads the stamina through the public properties below
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
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);

        // not sprinting right away again after recovering do only a small amount of stamina
        if (isExhausted && currentStamina >= restartStamina)
        {
            isExhausted = false;
        }
    }

    // The movement script calls this every frame
    // Pass true only when the player is actually sprinting
    public void SetSprinting(bool sprinting)
    {
        isSprinting = sprinting && CanSprint;
    }
}