using System;
using UnityEngine;

#if DISSONANCE
using Dissonance;
#endif

// Proximity sesli sohbet — Dissonance entegrasyonu.
// Dissonance ses altyapısı, oyuncular arasındaki 3D mesafeyi ve mekansal ses seviyesini (spatial audio)
// otomatik olarak hesaplar; bu sebeple eski Photon Voice'taki gibi manuel mesafe güncellemelerine gerek yoktur.
// Dissonance kurulu değilken projenin derlenebilmesi için kodlar #if DISSONANCE bloklarına alınmıştır.
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

    // Geriye dönük uyumluluk ve API yapısını bozmamak için stub'lar.
    // Oyuncu prefab'larındaki Dissonance bileşenleri kendilerini otomatik yönetecektir.
    public void RegisterLocal(int playerId, MonoBehaviour recorder) { }
    public void UnregisterLocal() { }
    public void RegisterRemoteSpeaker(int playerId, MonoBehaviour speaker) { }
    public void UnregisterRemoteSpeaker(int playerId) { }

    // Mikrofon kapatma / sessize alma — DissonanceComms üzerinden tüm iletimi susturur.
    public void SetTransmit(bool enabled)
    {
#if DISSONANCE
        var comms = FindFirstObjectByType<DissonanceComms>();
        if (comms != null)
        {
            comms.IsMuted = !enabled;
        }
#else
        Debug.Log($"[VoiceChatManager] SetTransmit({enabled}) çağrıldı fakat Dissonance kurulu değil.");
#endif
    }
}
