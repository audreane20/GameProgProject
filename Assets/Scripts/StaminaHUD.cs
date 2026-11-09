using UnityEngine;
using UnityEngine.UI; //for Slider and Image

//shows stamina stored by PlayerStamina
public class StaminaHUD : MonoBehaviour
{
    [Header("Stamina References")]
    [SerializeField] private PlayerStamina playerStamina;
    [SerializeField] private Slider staminaBar;
    [SerializeField] private Image staminaFill;

    [Header("Get Colors")]
    [SerializeField] private Color normalColor = Color.green;
    [SerializeField] private Color lowColor = Color.red;
    // GameObject is enough because we only show or hide the text:
    [SerializeField] private GameObject exhaustedText;

    private void Start()
    {
        // Match the slider's range to the player's stamina settings
        staminaBar.minValue = 0f;
        staminaBar.maxValue = playerStamina.MaxStamina;
    }

    private void Update()
    {
        UpdateStamina();
        // Show the warning while exhausted.
        // Hide it when enough stamina has recovered.
        exhaustedText.SetActive(playerStamina.IsExhausted);
    }

    private void UpdateStamina()
    {
        // Read the gameplay value and display it on the slider
        staminaBar.value = playerStamina.CurrentStamina;

        // Calculate 25 percent of maximum stamina
        float lowStamina = playerStamina.MaxStamina * 0.25f;

        // Color feedback makes low stamina easy to see
        if (playerStamina.CurrentStamina <= lowStamina)
        {
            staminaFill.color = lowColor;
        }
        else
        {
            staminaFill.color = normalColor;
        }
    }
}
