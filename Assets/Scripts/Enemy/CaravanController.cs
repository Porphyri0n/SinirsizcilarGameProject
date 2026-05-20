using System;
using UnityEngine;

// Ticari kervan beyni — CaravanData ile yapilandirilir, CaravanMovement ile yol alir.
// CaravanState ile durum yonetir: Approaching -> (UnderAttack) -> Arrived -> Departing.
// IDamageable: haydut saldirisinda can kaybeder; can 0 olursa kervan yok edilir, kargo kaybolur.
// Prep phase'de her 2 wave'de bir gelme zamanlamasi spawner'a (host) aittir; burada Launch ile baslatilir.
[RequireComponent(typeof(CaravanMovement))]
public class CaravanController : MonoBehaviour, IDamageable
{
    [SerializeField] private CaravanData data;
    [SerializeField] private CaravanMovement movement;

    private float currentHealth;
    private CaravanState state;
    private bool destroyed;
    private bool delivered;

    public CaravanState State => state;
    public CaravanData Data => data;

    // ── IDamageable ──────────────────────────────────────────────────────
    public float CurrentHealth => currentHealth;
    public float MaxHealth => data != null ? data.maxHealth : 0f;
    public bool IsAlive => !destroyed && currentHealth > 0f;
    public event Action OnDeath;

    private void Awake()
    {
        if (movement == null) movement = GetComponent<CaravanMovement>();
    }

    private void OnEnable()
    {
        if (movement != null)
        {
            movement.OnReachedCastle += HandleReachedCastle;
            movement.OnDeparted += HandleDeparted;
        }
    }

    private void OnDisable()
    {
        if (movement != null)
        {
            movement.OnReachedCastle -= HandleReachedCastle;
            movement.OnDeparted -= HandleDeparted;
        }
    }

    // Spawner kervani yapilandirip yolculugu baslatir.
    public void Launch(CaravanData caravanData)
    {
        data = caravanData;
        if (movement != null) movement.Configure(caravanData);

        currentHealth = MaxHealth;
        destroyed = false;
        delivered = false;

        state = CaravanState.Approaching;
        EventBus.FireCaravanApproaching(data);
        if (movement != null) movement.BeginApproach();
    }

    private void HandleReachedCastle()
    {
        if (!IsAlive || delivered) return;

        delivered = true;
        state = CaravanState.Arrived;
        EventBus.FireCaravanArrived(data);      // CaravanReceiver kargoyu burada teslim alir

        // Teslimden sonra geri donus
        state = CaravanState.Departing;
        if (movement != null) movement.BeginDepart();
    }

    private void HandleDeparted()
    {
        Destroy(gameObject);        // yol cikisina ulasti, sahneden kalkar
    }

    // ── IDamageable: haydut saldirisi ────────────────────────────────────
    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (!IsAlive || amount <= 0f) return;

        // Yolculuk sirasinda ilk hasarda saldiri altinda durumuna gec
        if (state == CaravanState.Approaching)
        {
            state = CaravanState.UnderAttack;
            EventBus.FireCaravanUnderAttack(transform.position);
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        if (currentHealth <= 0f)
            HandleDestroyed();
    }

    private void HandleDestroyed()
    {
        if (destroyed) return;
        destroyed = true;

        // Kargo kaybolur — teslim edilmediyse FireCaravanArrived hic cagrilmaz
        EventBus.FireCaravanDestroyed();
        OnDeath?.Invoke();
        Destroy(gameObject);
    }
}
