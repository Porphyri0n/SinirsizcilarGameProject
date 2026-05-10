using System;
using UnityEngine;
using Photon.Pun;
using ExitGames.Client.Photon;

// Oyun durumu senkronu — host'taki faz/dalga olaylarını RPC ile tüm client'lara taşır.
// Oda custom property'lerinde güncel durumu tutar (phase, wave, castleHP) — geç katılım için.
// Host EventBus event'i fire eder -> burada RPC'ye çevrilir -> client'larda EventBus tekrar fire edilir.
public class GameStateSync : MonoBehaviourPunCallbacks
{
    public static GameStateSync Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnEnable()
    {
        base.OnEnable();        // MonoBehaviourPunCallbacks callback kaydı
        EventBus.OnPhaseChanged += HandlePhaseChanged;
        EventBus.OnWaveStart += HandleWaveStart;
        EventBus.OnWaveEnd += HandleWaveEnd;
        EventBus.OnCastleDamaged += HandleCastleDamaged;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        EventBus.OnPhaseChanged -= HandlePhaseChanged;
        EventBus.OnWaveStart -= HandleWaveStart;
        EventBus.OnWaveEnd -= HandleWaveEnd;
        EventBus.OnCastleDamaged -= HandleCastleDamaged;
    }

    // ── Host: EventBus -> RPC / room property ───────────────────────────

    private void HandlePhaseChanged(GamePhase phase)
    {
        if (!CanBroadcast()) return;
        SetRoomProperty(NetworkKeys.ROOM_PHASE, (int)phase);
        photonView.RPC(NetworkKeys.RPC_PHASE_CHANGE, RpcTarget.Others, (int)phase);
    }

    private void HandleWaveStart(int waveNumber)
    {
        if (!CanBroadcast()) return;
        SetRoomProperty(NetworkKeys.ROOM_WAVE, waveNumber);
        photonView.RPC(NetworkKeys.RPC_WAVE_START, RpcTarget.Others, waveNumber);
    }

    private void HandleWaveEnd(int waveNumber)
    {
        if (!CanBroadcast()) return;
        photonView.RPC(NetworkKeys.RPC_WAVE_END, RpcTarget.Others, waveNumber);
    }

    private void HandleCastleDamaged(float current, float max)
    {
        if (!CanBroadcast()) return;
        SetRoomProperty(NetworkKeys.ROOM_CASTLE_HP, current);
    }

    // ── Client: RPC -> EventBus ─────────────────────────────────────────

    [PunRPC]
    public void RPC_PhaseChange(int phaseIndex) => EventBus.FirePhaseChanged((GamePhase)phaseIndex);

    [PunRPC]
    public void RPC_WaveStart(int waveNumber) => EventBus.FireWaveStart(waveNumber);

    [PunRPC]
    public void RPC_WaveEnd(int waveNumber) => EventBus.FireWaveEnd(waveNumber);

    // ── Client: room property -> EventBus ───────────────────────────────

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (AuthorityManager.IsHost) return;
        if (changedProps.TryGetValue(NetworkKeys.ROOM_CASTLE_HP, out object hp))
            EventBus.FireCastleDamaged((float)hp, GameConstants.CASTLE_MAX_HP);
    }

    // ── Yardımcılar ─────────────────────────────────────────────────────

    private static bool CanBroadcast() => PhotonNetwork.InRoom && AuthorityManager.IsHost;

    private static void SetRoomProperty(string key, object value)
    {
        if (PhotonNetwork.CurrentRoom == null) return;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { key, value } });
    }
}
