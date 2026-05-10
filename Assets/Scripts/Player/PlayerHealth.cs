using System;
using UnityEngine;

// Oyuncu canı. IDamageable implement eder.
// Can biterse OnDeath fire eder ve EventBus.FirePlayerDied ile herkese haber verir.
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private int playerID = -1;     // Photon ActorNumber — network katmanı atar

    private float currentHealth;
    private bool isDead;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsAlive => !isDead;

    public event Action OnDeath;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (isDead || amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (currentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        isDead = true;
        OnDeath?.Invoke();
        EventBus.FirePlayerDied(playerID, transform.position);
    }

    // Revive / spawn sonrası can'ı geri doldurur.
    public void ResetHealth()
    {
        isDead = false;
        currentHealth = maxHealth;
    }

    public void SetPlayerID(int id) => playerID = id;
}
