using System;
using UnityEngine;
using Unity.Netcode;

// Oyun durumu senkronu — host'taki faz/dalga olaylarını NetworkVariable ile tüm client'lara taşır.
// Geç katılan client'lar, NetworkVariable'ın başlangıç değerleriyle güncel durumu kurar.
public class GameStateSync : NetworkBehaviour
{
    public static GameStateSync Instance { get; private set; }

    private readonly NetworkVariable<int> roomPhase = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> roomWave = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> castleHP = new NetworkVariable<float>(GameConstants.CASTLE_MAX_HP, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> wall1HP = new NetworkVariable<float>(500f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> wall2HP = new NetworkVariable<float>(500f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> wall3HP = new NetworkVariable<float>(500f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> wall4HP = new NetworkVariable<float>(500f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public readonly NetworkVariable<bool> GameStarted = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Wall[] cachedWalls;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        RefreshWallCache();

        if (IsServer)
        {
            if (GameNetworkManager.Instance != null)
            {
                GameStarted.Value = GameNetworkManager.Instance.GameStarted;
            }
            
            // Initial wall health sync
            SyncAllWallsToServerVariables();
        }
        else
        {
            // Apply initial state for late joiners
            EventBus.FirePhaseChanged((GamePhase)roomPhase.Value);
            EventBus.FireWaveStart(roomWave.Value);
            
            if (CastleHealth.Instance != null)
                CastleHealth.Instance.SetHealth(castleHP.Value);
            else
                EventBus.FireCastleDamaged(castleHP.Value, GameConstants.CASTLE_MAX_HP);

            ApplyWallHealth(0, wall1HP.Value);
            ApplyWallHealth(1, wall2HP.Value);
            ApplyWallHealth(2, wall3HP.Value);
            ApplyWallHealth(3, wall4HP.Value);
        }

        roomPhase.OnValueChanged += OnPhaseChangedValue;
        roomWave.OnValueChanged += OnWaveChangedValue;
        castleHP.OnValueChanged += OnCastleHPChangedValue;
        
        wall1HP.OnValueChanged += (oldVal, newVal) => OnWallHPChangedValue(0, newVal);
        wall2HP.OnValueChanged += (oldVal, newVal) => OnWallHPChangedValue(1, newVal);
        wall3HP.OnValueChanged += (oldVal, newVal) => OnWallHPChangedValue(2, newVal);
        wall4HP.OnValueChanged += (oldVal, newVal) => OnWallHPChangedValue(3, newVal);
    }

    private void RefreshWallCache()
    {
        cachedWalls = FindObjectsByType<Wall>(FindObjectsSortMode.None);
        Array.Sort(cachedWalls, (a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
    }

    private void SyncAllWallsToServerVariables()
    {
        if (!IsServer || cachedWalls == null) return;
        if (cachedWalls.Length > 0) wall1HP.Value = cachedWalls[0].CurrentHealth;
        if (cachedWalls.Length > 1) wall2HP.Value = cachedWalls[1].CurrentHealth;
        if (cachedWalls.Length > 2) wall3HP.Value = cachedWalls[2].CurrentHealth;
        if (cachedWalls.Length > 3) wall4HP.Value = cachedWalls[3].CurrentHealth;
    }

    private void ApplyWallHealth(int index, float health)
    {
        if (cachedWalls == null || index < 0 || index >= cachedWalls.Length) return;
        if (cachedWalls[index] != null)
        {
            cachedWalls[index].SetHealth(health);
        }
    }

    public override void OnNetworkDespawn()
    {
        roomPhase.OnValueChanged -= OnPhaseChangedValue;
        roomWave.OnValueChanged -= OnWaveChangedValue;
        castleHP.OnValueChanged -= OnCastleHPChangedValue;
    }

    public void OnEnable()
    {
        EventBus.OnPhaseChanged += HandlePhaseChanged;
        EventBus.OnWaveStart += HandleWaveStart;
        EventBus.OnWaveEnd += HandleWaveEnd;
        EventBus.OnCastleDamaged += HandleCastleDamaged;
        EventBus.OnWallHealthChanged += HandleWallHealthChanged;
        EventBus.OnShipDestroyed += HandleShipDestroyed;
        EventBus.OnResourceReceived += HandleResourceReceived;
    }

    public void OnDisable()
    {
        EventBus.OnPhaseChanged -= HandlePhaseChanged;
        EventBus.OnWaveStart -= HandleWaveStart;
        EventBus.OnWaveEnd -= HandleWaveEnd;
        EventBus.OnCastleDamaged -= HandleCastleDamaged;
        EventBus.OnWallHealthChanged -= HandleWallHealthChanged;
        EventBus.OnShipDestroyed -= HandleShipDestroyed;
        EventBus.OnResourceReceived -= HandleResourceReceived;
    }

    // ── Host: EventBus -> NetworkVariable / RPC ───────────────────────────

    private void HandlePhaseChanged(GamePhase phase)
    {
        if (IsServer)
            roomPhase.Value = (int)phase;
    }

    private void HandleWaveStart(int waveNumber)
    {
        if (IsServer)
        {
            roomWave.Value = waveNumber;
            BroadcastWaveStartRpc(waveNumber);
        }
    }

    private void HandleWaveEnd(int waveNumber)
    {
        if (IsServer)
            BroadcastWaveEndRpc(waveNumber);
    }

    private void HandleCastleDamaged(float current, float max)
    {
        if (IsServer)
            castleHP.Value = current;
    }

    private void HandleWallHealthChanged(int index, float current, float max)
    {
        if (!IsServer) return;

        switch (index)
        {
            case 0: wall1HP.Value = current; break;
            case 1: wall2HP.Value = current; break;
            case 2: wall3HP.Value = current; break;
            case 3: wall4HP.Value = current; break;
        }
    }

    private void HandleShipDestroyed(ShipType type, Vector3 pos)
    {
        if (IsServer)
            BroadcastShipDestroyedRpc(type, pos);
    }

    private void HandleResourceReceived(ResourceType type, int amount)
    {
        if (IsServer)
            BroadcastResourceReceivedRpc(type, amount);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestRepairWallServerRpc(int index)
    {
        if (cachedWalls == null || index < 0 || index >= cachedWalls.Length) return;
        Wall wall = cachedWalls[index];
        if (wall == null || !wall.NeedsRepair) return;

        // Check resources on server
        EconomyManager economy = EconomyManager.Instance;
        if (economy == null) return;

        bool canAfford = true;
        foreach (RecipeIngredient ingredient in wall.FullRepairCost)
        {
            if (!economy.HasEnough(ingredient.resourceType, ingredient.amount))
            {
                canAfford = false;
                break;
            }
        }

        if (canAfford)
        {
            // Spend resources on server
            foreach (RecipeIngredient ingredient in wall.FullRepairCost)
                economy.SpendResource(ingredient.resourceType, ingredient.amount);

            // Repair on server
            wall.Repair(wall.MaxHealth);
            
            // Sync is handled by HandleWallHealthChanged -> NetworkVariable
        }
    }

    // ── RPCs: Server -> Everyone ─────────────────────────────────────────

    [Rpc(SendTo.Everyone)]
    private void BroadcastWaveStartRpc(int waveNumber)
    {
        if (!IsServer) EventBus.FireWaveStart(waveNumber);
    }

    [Rpc(SendTo.Everyone)]
    private void BroadcastWaveEndRpc(int waveNumber)
    {
        if (!IsServer) EventBus.FireWaveEnd(waveNumber);
    }

    [Rpc(SendTo.Everyone)]
    private void BroadcastShipDestroyedRpc(ShipType type, Vector3 pos)
    {
        if (!IsServer) EventBus.FireShipDestroyed(type, pos);
    }

    [Rpc(SendTo.Everyone)]
    private void BroadcastResourceReceivedRpc(ResourceType type, int amount)
    {
        if (!IsServer) EventBus.FireResourceReceived(type, amount);
    }

    // ── Client: NetworkVariable OnValueChanged -> EventBus ──────────────

    private void OnPhaseChangedValue(int oldVal, int newVal)
    {
        if (!IsServer)
            EventBus.FirePhaseChanged((GamePhase)newVal);
    }

    private void OnWaveChangedValue(int oldVal, int newVal)
    {
        // WaveStart is handled by BroadcastWaveStartRpc for active players.
        // For late joiners, we could fire it here if the value is non-zero.
        if (!IsServer && oldVal == 0 && newVal > 0)
            EventBus.FireWaveStart(newVal);
    }

    private void OnCastleHPChangedValue(float oldVal, float newVal)
    {
        if (!IsServer)
        {
            if (CastleHealth.Instance != null)
                CastleHealth.Instance.SetHealth(newVal);
            else
                EventBus.FireCastleDamaged(newVal, GameConstants.CASTLE_MAX_HP);
        }
    }

    private void OnWallHPChangedValue(int index, float health)
    {
        if (!IsServer)
        {
            ApplyWallHealth(index, health);
            
            // Also notify CastleWalls to update UI
            float max = (cachedWalls != null && index >= 0 && index < cachedWalls.Length && cachedWalls[index] != null) 
                        ? cachedWalls[index].MaxHealth : 500f;
            EventBus.FireWallHealthChanged(index, health, max);
        }
    }
}
