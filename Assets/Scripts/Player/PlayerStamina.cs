using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    private float currentStamina;
    private float maxStamina = GameConstants.PLAYER_MAX_STAMINA;

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float StaminaPercent => maxStamina > 0 ? currentStamina / maxStamina : 0f;

    private void Awake()
    {
        currentStamina = maxStamina;
    }

    public void ConsumeStamina(float amount)
    {
        currentStamina = Mathf.Max(0f, currentStamina - amount);
    }

    public void RegenerateStamina(float amount)
    {
        currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
    }

    public bool HasStamina => currentStamina > 0.1f;
}
