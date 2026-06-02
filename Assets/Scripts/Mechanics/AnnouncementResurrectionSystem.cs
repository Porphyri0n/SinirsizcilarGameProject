using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Dissonance;
using System.Linq;

/// <summary>
/// Anons etiketi (Tag: Anons) olan trigger collider'a giren oyuncunun 5 saniye beklemesiyle 
/// rastgele bir ölü oyuncuyu dirilten sistem. Diriltme sırasında oyuncu Global ses kanalına geçer.
/// </summary>
public class AnnouncementResurrectionSystem : NetworkBehaviour
{
    public static AnnouncementResurrectionSystem Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float requiredDuration = 5f;
    [SerializeField] private string announcementTag = "Anons";

    private float timer = 0f;
    private bool isLocalPlayerInTrigger = false;
    private VoiceProximityBroadcastTrigger proximityTrigger;
    private VoiceBroadcastTrigger globalTrigger;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (!isLocalPlayerInTrigger) return;

        timer += Time.deltaTime;
        if (timer >= requiredDuration)
        {
            timer = 0f;
            Debug.Log("[Announcement] Timer reached. Requesting random resurrection...");
            ReviveRandomPlayerServerRpc();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        var networkObject = other.GetComponent<NetworkObject>();
        if (networkObject == null || !networkObject.IsLocalPlayer) return;

        // Script trigger collider'a sahip nesne üzerindeyse tag kontrolü yapıyoruz.
        if (!gameObject.CompareTag(announcementTag)) return;

        StartAnnouncement(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        var networkObject = other.GetComponent<NetworkObject>();
        if (networkObject == null || !networkObject.IsLocalPlayer) return;

        StopAnnouncement();
    }

    private void StartAnnouncement(GameObject player)
    {
        isLocalPlayerInTrigger = true;
        timer = 0f;

        proximityTrigger = player.GetComponent<VoiceProximityBroadcastTrigger>();
        globalTrigger = player.GetComponent<VoiceBroadcastTrigger>();

        // Ses kanalı değişimi: Proximity kapat, Global aç.
        if (proximityTrigger != null) proximityTrigger.enabled = false;
        if (globalTrigger != null) globalTrigger.enabled = true;
        
        Debug.Log("[Announcement] Player entered trigger. Global broadcast enabled.");
    }

    private void StopAnnouncement()
    {
        isLocalPlayerInTrigger = false;
        timer = 0f;

        // Ses kanalı değişimi: Proximity aç, Global kapat.
        if (proximityTrigger != null) proximityTrigger.enabled = true;
        if (globalTrigger != null) globalTrigger.enabled = false;
        
        Debug.Log("[Announcement] Player left trigger. Reverted to proximity broadcast.");
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReviveRandomPlayerServerRpc()
    {
        // Tüm PlayerHealth bileşenlerini bul (NGO'da NetworkObject üzerinden clientID'lere ulaşıyoruz)
        var allHealth = Object.FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        var deadPlayers = allHealth.Where(p => !p.IsAlive).ToList();

        if (deadPlayers.Count > 0)
        {
            // Rastgele birini seç
            var target = deadPlayers[Random.Range(0, deadPlayers.Count)];
            var netObj = target.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                int id = (int)netObj.OwnerClientId;
                
                // Sunucu tarafında canını doldur (Eğer NetworkVariable değilse ClientRpc'de de dolmalı)
                target.ResetHealth();
                
                // Tüm client'lara diriltme bilgisini gönder
                RevivePlayerClientRpc(id);
                Debug.Log($"[Announcement Server] Reviving random player ID: {id}");
            }
        }
        else
        {
            Debug.Log("[Announcement Server] No dead players to revive.");
        }
    }

    [ClientRpc]
    private void RevivePlayerClientRpc(int playerID)
    {
        // EventBus üzerinden sistemleri (SpawnController, UI vb.) tetikle
        EventBus.FirePlayerRevived(playerID);
    }
}
