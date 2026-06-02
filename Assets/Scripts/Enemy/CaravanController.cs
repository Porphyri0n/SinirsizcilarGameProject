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
    public event Action<float, float> OnHealthChanged;

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

    private void Start()
    {
        if (GetComponent<DamageFlashEffect>() == null)
        {
            gameObject.AddComponent<DamageFlashEffect>();
        }

        GameObject healthBarPrefab = Resources.Load<GameObject>("WorldHealthBar");
        if (healthBarPrefab != null)
        {
            Instantiate(healthBarPrefab, transform);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        netHealth.OnValueChanged += OnHealthValueChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        netHealth.OnValueChanged -= OnHealthValueChanged;
    }

    private void OnHealthValueChanged(float previousValue, float newValue)
    {
        OnHealthChanged?.Invoke(newValue, MaxHealth);
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
        if (!IsServer || !IsAlive || netInteracted.Value) return;

        // Kaleye vardi: burada DUR ve bekle. Otomatik teslim/geri donus YOK.
        // Oyuncu E ile lootlayana ya da haydutlar yok edene kadar burada bekler.
        netState.Value = CaravanState.Arrived;
        if (movement != null) movement.StopMoving();
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

    [Rpc(SendTo.Server)]
    public void RequestTakeDamageRpc(float amount, Vector3 hitPoint)
    {
        TakeDamage(amount, hitPoint);
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
    public bool CanInteract(GameObject player) => IsAlive && !netInteracted.Value && !netDelivered.Value && netState.Value == CaravanState.Arrived;

    public void Interact(GameObject player)
    {
        if (netInteracted.Value) return;
        
        Debug.Log("[Caravan] Interact requested locally.");
        RequestInteractionServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestInteractionServerRpc()
    {
        if (netInteracted.Value || netDelivered.Value || !IsAlive || netState.Value != CaravanState.Arrived) return;
        netInteracted.Value = true;

        // Kervanin GERCEK kargosunu encode edip tum client'lara teslim et.
        // Self-contained: sahnede CaravanReceiver olmasa da loot calisir.
        if (data != null && data.cargo != null)
        {
            int[] types = new int[data.cargo.Length];
            int[] amounts = new int[data.cargo.Length];
            int idx = 0;
            foreach (CaravanCargoEntry entry in data.cargo)
            {
                if (entry == null || entry.amount <= 0) continue;
                types[idx] = (int)entry.resourceType;
                amounts[idx] = entry.amount;
                idx++;
            }
            if (idx > 0)
            {
                Array.Resize(ref types, idx);
                Array.Resize(ref amounts, idx);
                DeliverCargoClientRpc(types, amounts);

                // Sunucu tarafında kaynakları doğrudan ekle (authoritative)
                for (int i = 0; i < idx; i++)
                {
                    EconomyManager.Instance.AddResource((ResourceType)types[i], amounts[i]);
                }
            }
        }

        Debug.Log("[Caravan Server] Looted — kargo teslim edildi, geri donuluyor.");

        // Lootlandi: TakeDamage artik no-op (dayaniksiz degil) ve kervan geri doner.
        netState.Value = CaravanState.Departing;
        if (movement != null) movement.BeginDepart();
    }

    [ClientRpc]
    private void DeliverCargoClientRpc(int[] types, int[] amounts)
    {
        int count = Mathf.Min(types.Length, amounts.Length);
        for (int i = 0; i < count; i++)
        {
            ResourceType type = (ResourceType)types[i];
            EventBus.FireResourceReceived(type, amounts[i]);
            Debug.Log($"<color=green>[Kervan]</color> {amounts[i]} {type} alindi!");
        }
    }
}
