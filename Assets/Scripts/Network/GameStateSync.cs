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

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
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
    }

    public void OnDisable()
    {
        EventBus.OnPhaseChanged -= HandlePhaseChanged;
        EventBus.OnWaveStart -= HandleWaveStart;
        EventBus.OnWaveEnd -= HandleWaveEnd;
        EventBus.OnCastleDamaged -= HandleCastleDamaged;
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
            roomWave.Value = waveNumber;
    }

    private void HandleWaveEnd(int waveNumber)
    {
        if (IsServer)
            RPC_WaveEndRpc(waveNumber);
    }

    private void HandleCastleDamaged(float current, float max)
    {
        if (IsServer)
            castleHP.Value = current;
    }

    // ── Client: RPC -> EventBus ─────────────────────────────────────────

    [Rpc(SendTo.NotOwner)]
    private void RPC_WaveEndRpc(int waveNumber) => EventBus.FireWaveEnd(waveNumber);

    // ── Client: NetworkVariable OnValueChanged -> EventBus ──────────────

    private void OnPhaseChangedValue(int oldVal, int newVal)
    {
        if (!IsServer)
            EventBus.FirePhaseChanged((GamePhase)newVal);
    }

    private void OnWaveChangedValue(int oldVal, int newVal)
    {
        if (!IsServer)
            EventBus.FireWaveStart(newVal);
    }

    private void OnCastleHPChangedValue(float oldVal, float newVal)
    {
        if (!IsServer)
            EventBus.FireCastleDamaged(newVal, GameConstants.CASTLE_MAX_HP);
    }
}
