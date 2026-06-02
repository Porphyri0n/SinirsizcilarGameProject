using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    private float currentStamina;
    private float maxStamina = GameConstants.PLAYER_MAX_STAMINA;

    private bool isExhausted;

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float StaminaPercent => maxStamina > 0 ? currentStamina / maxStamina : 0f;
    public bool IsExhausted => isExhausted;

    private void Awake()
    {
        currentStamina = maxStamina;
    }

    public void ConsumeStamina(float amount)
    {
        currentStamina = Mathf.Max(0f, currentStamina - amount);
        if (currentStamina <= 0.01f)
        {
            isExhausted = true;
        }
    }

    public void RegenerateStamina(float amount)
    {
        currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
        
        // Yorunluktan cikmak icin en az %20 stamina lazim
        if (isExhausted && currentStamina >= maxStamina * 0.2f)
        {
            isExhausted = false;
        }
    }

    public bool HasStamina => !isExhausted && currentStamina > 1f;
}
