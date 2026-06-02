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
    public readonly NetworkVariable<bool> GameStarted = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (GameNetworkManager.Instance != null)
            {
                GameStarted.Value = GameNetworkManager.Instance.GameStarted;
            }
        }
        else
        {
            // Apply initial state for late joiners
            EventBus.FirePhaseChanged((GamePhase)roomPhase.Value);
            EventBus.FireWaveStart(roomWave.Value);
            EventBus.FireCastleDamaged(castleHP.Value, GameConstants.CASTLE_MAX_HP);
        }

        roomPhase.OnValueChanged += OnPhaseChangedValue;
        roomWave.OnValueChanged += OnWaveChangedValue;
        castleHP.OnValueChanged += OnCastleHPChangedValue;
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
        EventBus.OnShipDestroyed += HandleShipDestroyed;
        EventBus.OnResourceReceived += HandleResourceReceived;
    }

    public void OnDisable()
    {
        EventBus.OnPhaseChanged -= HandlePhaseChanged;
        EventBus.OnWaveStart -= HandleWaveStart;
        EventBus.OnWaveEnd -= HandleWaveEnd;
        EventBus.OnCastleDamaged -= HandleCastleDamaged;
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
            EventBus.FireCastleDamaged(newVal, GameConstants.CASTLE_MAX_HP);
    }
}
