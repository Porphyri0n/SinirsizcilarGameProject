using System;
using System.Collections.Generic;
using UnityEngine;

// Proximity (yakınlık) sesli sohbet hesaplayıcısı. Singleton.
// Konuşan ile dinleyen arası mesafeye göre ses seviyesi (0..1) verir; ses motoru (VoiceChatManager) bunu kullanır.
// Kuledeki oyuncunun menzili TOWER_VOICE_RANGE — pratikte tüm haritaya ulaşır.
public class ProximityChatManager : MonoBehaviour
{
    public static ProximityChatManager Instance { get; private set; }

    private readonly Dictionary<int, Transform> players = new Dictionary<int, Transform>();
    private readonly HashSet<int> playersInTower = new HashSet<int>();

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

    // Oyuncular doğunca kaydolur, ölünce/çıkınca silinir
    public void RegisterPlayer(int playerId, Transform playerTransform) => players[playerId] = playerTransform;

    public void UnregisterPlayer(int playerId)
    {
        players.Remove(playerId);
        playersInTower.Remove(playerId);
    }

    public bool IsInTower(int playerId) => playersInTower.Contains(playerId);

    // İki kayıtlı oyuncu arası ses seviyesi
    public float GetVoiceVolume(int speakerId, int listenerId)
    {
        if (!players.TryGetValue(speakerId, out Transform speaker)) return 0f;
        if (!players.TryGetValue(listenerId, out Transform listener)) return 0f;
        return GetVoiceVolume(speakerId, speaker.position, listener.position);
    }

    // Konuşanın menziline ve mesafeye göre ses seviyesi: yakında tam, falloff'tan sonra azalır, menzil dışında 0
    public float GetVoiceVolume(int speakerId, Vector3 speakerPos, Vector3 listenerPos)
    {
        float range = IsInTower(speakerId) ? GameConstants.TOWER_VOICE_RANGE : GameConstants.VOICE_BASE_RANGE;
        float distance = Vector3.Distance(speakerPos, listenerPos);

        if (distance >= range) return 0f;
        if (distance <= GameConstants.VOICE_FALLOFF_START) return 1f;

        return Mathf.Clamp01(1f - distance / range);
    }

    private void HandleTowerEntered(int playerId, DefenseType towerType) => playersInTower.Add(playerId);
    private void HandleTowerExited(int playerId, DefenseType towerType) => playersInTower.Remove(playerId);
}
