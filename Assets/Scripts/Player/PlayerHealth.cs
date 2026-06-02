using System;
using UnityEngine;
using Unity.Netcode;

// Oyuncu canı. IDamageable implement eder.
// Can biterse OnDeath fire eder ve EventBus.FirePlayerDied ile herkese haber verir.
public class PlayerHealth : NetworkBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private int playerID = -1;     // Client ID — network katmanı atar

    private readonly NetworkVariable<float> netHealth = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> netIsDead = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public float CurrentHealth => netHealth.Value;
    public float MaxHealth => maxHealth;
    public bool IsAlive => !netIsDead.Value;

    public event Action OnDeath;

    private void Awake()
    {
        // NetworkVariable'lar OnNetworkSpawn'da initialize edilir.
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            netHealth.Value = maxHealth;
            netIsDead.Value = false;
        }

        netIsDead.OnValueChanged += (oldVal, newVal) => {
            if (newVal && !oldVal)
            {
                OnDeath?.Invoke();
                EventBus.FirePlayerDied(playerID, transform.position);
            }
        };
        
        // netHealth.OnValueChanged hooked if needed (e.g. for UI)
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (!IsServer) return;
        if (netIsDead.Value || amount <= 0f) return;

        netHealth.Value = Mathf.Max(0f, netHealth.Value - amount);
        if (netHealth.Value <= 0f)
            Die();
    }

    private void Die()
    {
        if (!IsServer) return;
        netIsDead.Value = true;
    }

    // Revive / spawn sonrası can'ı geri doldurur.
    public void ResetHealth()
    {
        if (!IsServer) return;
        netIsDead.Value = false;
        netHealth.Value = maxHealth;
    }

    public void SetPlayerID(int id) => playerID = id;

    // Can doldurma (Heal) metodu
    public void Heal(float amount)
    {
        if (!IsServer) return;
        if (netIsDead.Value || amount <= 0f) return;

        netHealth.Value = Mathf.Min(maxHealth, netHealth.Value + amount);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestHealServerRpc(float amount)
    {
        Heal(amount);
    }
}
