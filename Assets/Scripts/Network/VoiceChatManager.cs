using System;
using UnityEngine;
using Dissonance;

// Sesli sohbet yönetimi — Dissonance entegrasyonu.
// Dissonance + VoiceProximityBroadcastTrigger/VoiceProximityReceiptTrigger
// proximity ses mesafesini otomatik olarak yönetir.
// Bu sınıf yalnızca global mute/unmute kontrolü sağlar.
public class VoiceChatManager : MonoBehaviour
{
    public static VoiceChatManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Mikrofon iletimini aç/kapat — DissonanceComms üzerinden tüm iletimi susturur veya açar.
    /// </summary>
    public void SetTransmit(bool enabled)
    {
        var comms = FindFirstObjectByType<DissonanceComms>();
        if (comms != null)
        {
            comms.IsMuted = !enabled;
        }
        else
        {
            Debug.LogWarning($"[VoiceChatManager] SetTransmit({enabled}) çağrıldı fakat DissonanceComms bulunamadı.");
        }
    }

    /// <summary>
    /// Oyuncunun kulaklığını kapat/aç — tüm gelen sesi susturur.
    /// </summary>
    public void SetDeafen(bool deafened)
    {
        var comms = FindFirstObjectByType<DissonanceComms>();
        if (comms != null)
        {
            comms.IsDeafened = deafened;
        }
    }
}
