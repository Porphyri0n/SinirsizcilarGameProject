using System;
using System.Collections.Generic;
using UnityEngine;
using Dissonance;

// Proximity sesli sohbet yöneticisi — Singleton.
// Dissonance'ın VoiceProximityBroadcastTrigger/VoiceProximityReceiptTrigger bileşenleri
// mesafe tabanlı grid proximity'yi otomatik yönetir; bu sınıf yalnızca
// kule boost'u (TOWER_VOICE_RANGE) için trigger Range'ini runtime'da değiştirir.
public class ProximityChatManager : MonoBehaviour
{
    public static ProximityChatManager Instance { get; private set; }

    // playerId → (broadcast trigger, receipt trigger) — runtime referansları
    private readonly Dictionary<int, PlayerVoiceTriggers> players = new Dictionary<int, PlayerVoiceTriggers>();
    private readonly HashSet<int> playersInTower = new HashSet<int>();

    private struct PlayerVoiceTriggers
    {
        public VoiceProximityBroadcastTrigger broadcast;
        public VoiceProximityReceiptTrigger receipt;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        EventBus.OnTowerEntered += HandleTowerEntered;
        EventBus.OnTowerExited += HandleTowerExited;
    }

    private void OnDisable()
    {
        EventBus.OnTowerEntered -= HandleTowerEntered;
        EventBus.OnTowerExited -= HandleTowerExited;
    }

    // Oyuncu spawn olduğunda PlayerNetSync çağırır.
    // Transform'dan Dissonance proximity trigger bileşenlerini bulur.
    public void RegisterPlayer(int playerId, Transform playerTransform)
    {
        var triggers = new PlayerVoiceTriggers
        {
            broadcast = playerTransform.GetComponent<VoiceProximityBroadcastTrigger>(),
            receipt = playerTransform.GetComponent<VoiceProximityReceiptTrigger>()
        };
        players[playerId] = triggers;

        // Eğer oyuncu zaten kulede spawn olduysa (reconnect vb.) range'i ayarla
        if (playersInTower.Contains(playerId))
            ApplyRange(playerId, GameConstants.TOWER_VOICE_RANGE);
    }

    public void UnregisterPlayer(int playerId)
    {
        players.Remove(playerId);
        playersInTower.Remove(playerId);
    }

    public bool IsInTower(int playerId) => playersInTower.Contains(playerId);

    // Geriye dönük uyumluluk — Dissonance artık bunu otomatik yönetiyor.
    // Dışarıdan çağrılırsa basit mesafe hesabı döndürür.
    public float GetVoiceVolume(int speakerId, int listenerId)
    {
        if (!players.TryGetValue(speakerId, out var speakerT)) return 0f;
        if (!players.TryGetValue(listenerId, out var listenerT)) return 0f;
        if (speakerT.broadcast == null || listenerT.receipt == null) return 0f;
        return GetVoiceVolume(speakerId, speakerT.broadcast.transform.position, listenerT.receipt.transform.position);
    }

    public float GetVoiceVolume(int speakerId, Vector3 speakerPos, Vector3 listenerPos)
    {
        float range = IsInTower(speakerId) ? GameConstants.TOWER_VOICE_RANGE : GameConstants.VOICE_BASE_RANGE;
        float distance = Vector3.Distance(speakerPos, listenerPos);

        if (distance >= range) return 0f;
        if (distance <= GameConstants.VOICE_FALLOFF_START) return 1f;

        float falloffRange = range - GameConstants.VOICE_FALLOFF_START;
        return Mathf.Clamp01((range - distance) / falloffRange);
    }

    // ── Kule Event Handler'ları ──────────────────────────────────────────

    private void HandleTowerEntered(int playerId, DefenseType towerType)
    {
        playersInTower.Add(playerId);
        ApplyRange(playerId, GameConstants.TOWER_VOICE_RANGE);
    }

    private void HandleTowerExited(int playerId, DefenseType towerType)
    {
        playersInTower.Remove(playerId);
        ApplyRange(playerId, GameConstants.VOICE_BASE_RANGE);
    }

    // Dissonance trigger'larının Range'ini günceller — kule boost/normal geçişi.
    // Range int olduğu için Mathf.RoundToInt ile çeviriyoruz.
    private void ApplyRange(int playerId, float range)
    {
        if (!players.TryGetValue(playerId, out var triggers)) return;

        int rangeInt = Mathf.RoundToInt(range);

        if (triggers.broadcast != null)
            triggers.broadcast.Range = rangeInt;

        if (triggers.receipt != null)
            triggers.receipt.Range = rangeInt;
    }
}
