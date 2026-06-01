using System;
using UnityEngine;
using Unity.Netcode;

// Ticari kervan beyni — CaravanData ile yapilandirilir, CaravanMovement ile yol alir.
// CaravanState ile durum yonetir: Approaching -> (UnderAttack) -> Arrived -> Departing.
// IDamageable: haydut saldirisinda can kaybeder; can 0 olursa kervan yok edilir, kargo kaybolur.
[RequireComponent(typeof(CaravanMovement))]
public class CaravanController : NetworkBehaviour, IDamageable, IInteractable
{
    [SerializeField] private CaravanData data;
    [SerializeField] private CaravanMovement movement;

    private readonly NetworkVariable<float> netHealth = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<CaravanState> netState = new NetworkVariable<CaravanState>(CaravanState.Approaching, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> netDelivered = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> netInteracted = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> netDestroyed = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public CaravanState State => netState.Value;
    public CaravanData Data => data;

    // ── IDamageable ──────────────────────────────────────────────────────
    public float CurrentHealth => netHealth.Value;
    public float MaxHealth => data != null ? data.maxHealth : 0f;
    public bool IsAlive => !netDestroyed.Value && netHealth.Value > 0f;
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

    // Spawner kervani yapilandirip yolculugu baslatir. wave: kargo olceklemesi icin guncel wave.
    public void Launch(CaravanData caravanData, int wave)
    {
        data = BuildRuntimeData(caravanData, wave);
        if (movement != null) movement.Configure(data);

        if (IsServer)
        {
            netHealth.Value = MaxHealth;
            netDestroyed.Value = false;
            netDelivered.Value = false;
            netInteracted.Value = false;
            netState.Value = CaravanState.Approaching;
        }

        EventBus.FireCaravanApproaching(data);
        if (movement != null) movement.BeginApproach();
    }

    // Paylasilan SO'yu bozmadan runtime kopya uretir; kargoyu wave'e gore doldurur (gelismis kaynak sistemi).
    private CaravanData BuildRuntimeData(CaravanData baseData, int wave)
    {
        if (baseData == null) return null;

        CaravanData runtime = Instantiate(baseData);
        runtime.cargo = CaravanCargoBuilder.Build(wave, baseData.minWaveForAdvanced);
        return runtime;
    }

    private void HandleReachedCastle()
    {
        if (!IsServer || !IsAlive || netDelivered.Value || netInteracted.Value) return;

        netDelivered.Value = true;
        netState.Value = CaravanState.Arrived;
        EventBus.FireCaravanArrived(data);      // CaravanReceiver kargoyu burada teslim alir

        // Teslimden sonra geri donus
        netState.Value = CaravanState.Departing;
        if (movement != null) movement.BeginDepart();
    }

    private void HandleDeparted()
    {
        if (IsServer)
        {
            if (NetworkObject != null && NetworkObject.IsSpawned)
                NetworkObject.Despawn();
            else
                Destroy(gameObject);
        }
    }

    // ── IDamageable: haydut saldirisi ────────────────────────────────────
    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (!IsServer || !IsAlive || amount <= 0f || netInteracted.Value) return;

        // Yolculuk sirasinda ilk hasarda saldiri altinda durumuna gec
        if (netState.Value == CaravanState.Approaching)
        {
            netState.Value = CaravanState.UnderAttack;
            EventBus.FireCaravanUnderAttack(transform.position);
        }

        netHealth.Value = Mathf.Max(0f, netHealth.Value - amount);

        if (netHealth.Value <= 0f)
            HandleDestroyed();
    }

    private void HandleDestroyed()
    {
        if (!IsServer || netDestroyed.Value) return;
        netDestroyed.Value = true;

        // Kargo kaybolur — teslim edilmediyse FireCaravanArrived hic cagrilmaz
        EventBus.FireCaravanDestroyed();
        OnDeath?.Invoke();
        
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
        else
            Destroy(gameObject);
    }

    // ── IInteractable ────────────────────────────────────────────────────
    public string GetInteractPrompt() => "[E] Kaynak Al";
    public bool CanInteract(GameObject player) => IsAlive && !netInteracted.Value && !netDelivered.Value;

    public void Interact(GameObject player)
    {
        if (netInteracted.Value) return;
        
        Debug.Log("[Caravan] Interact requested locally.");
        RequestInteractionServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestInteractionServerRpc()
    {
        if (netInteracted.Value || netDelivered.Value || !IsAlive) return;
        netInteracted.Value = true;

        // Rastgele kaynak ve miktar
        ResourceType[] types = { ResourceType.Wood, ResourceType.Stone, ResourceType.Iron };
        ResourceType type = types[UnityEngine.Random.Range(0, types.Length)];
        int amount = UnityEngine.Random.Range(10, 26); // 10-25 arası

        // Tüm client'lara bildir (Economy Sync için)
        NotifyResourceReceivedClientRpc(type, amount);
        
        Debug.Log($"[Caravan Server] Interaction processed. Despawning...");

        // Yok et
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
    }

    [ClientRpc]
    private void NotifyResourceReceivedClientRpc(ResourceType type, int amount)
    {
        EventBus.FireResourceReceived(type, amount);
        Debug.Log($"<color=green>[Kervan]</color> {amount} {type} alındı!");
    }
}
