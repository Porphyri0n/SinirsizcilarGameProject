using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Voice.Unity;

// Proximity sesli sohbet — Photon Voice 2 ile entegrasyon.
// Local oyuncu: Recorder iletim acik; uzak oyuncular: Speaker'larin AudioSource volume'u
// her tick ProximityChatManager.GetVoiceVolume'dan gelen degere set edilir.
// Kuledeki konusmaci TOWER_VOICE_RANGE ile haritanin her yerine ulasir
// (mesafe hesabi ProximityChatManager'da, biz sadece volume'u uygulariz).
public class VoiceChatManager : MonoBehaviour
{
    public static VoiceChatManager Instance { get; private set; }

    [SerializeField] private float updateInterval = GameConstants.NETWORK_SYNC_RATE;

    private readonly Dictionary<int, Speaker> speakers = new Dictionary<int, Speaker>();
    private readonly Dictionary<int, AudioSource> sources = new Dictionary<int, AudioSource>();

    private Recorder localRecorder;
    private int localPlayerId = -1;
    private float nextUpdateAt;

    public int LocalPlayerId => localPlayerId;
    public bool HasLocalRecorder => localRecorder != null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Yerel oyuncu spawn olunca PlayerNetSync buradan kayit yapar.
    public void RegisterLocal(int playerId, Recorder recorder)
    {
        localPlayerId = playerId;
        localRecorder = recorder;
        if (recorder != null) recorder.TransmitEnabled = true;
    }

    public void UnregisterLocal()
    {
        if (localRecorder != null) localRecorder.TransmitEnabled = false;
        localRecorder = null;
        localPlayerId = -1;
    }

    // Uzak oyuncunun Speaker'i (prefab uzerinde) spawn olunca buradan kayit yapar.
    public void RegisterRemoteSpeaker(int playerId, Speaker speaker)
    {
        if (speaker == null) return;
        speakers[playerId] = speaker;

        AudioSource src = speaker.GetComponent<AudioSource>();
        if (src != null) sources[playerId] = src;
    }

    public void UnregisterRemoteSpeaker(int playerId)
    {
        speakers.Remove(playerId);
        sources.Remove(playerId);
    }

    // Mikrofon kapatma — eldeki Recorder iletimi keser (ornek: oyuncu olunce)
    public void SetTransmit(bool enabled)
    {
        if (localRecorder != null) localRecorder.TransmitEnabled = enabled;
    }

    private void Update()
    {
        if (Time.time < nextUpdateAt) return;
        nextUpdateAt = Time.time + Mathf.Max(0.02f, updateInterval);

        ProximityChatManager chat = ProximityChatManager.Instance;
        if (chat == null || localPlayerId < 0) return;

        foreach (KeyValuePair<int, AudioSource> kv in sources)
        {
            int speakerId = kv.Key;
            AudioSource src = kv.Value;
            if (src == null) continue;
            if (speakerId == localPlayerId) { src.volume = 0f; continue; }   // kendini duyma

            src.volume = chat.GetVoiceVolume(speakerId, localPlayerId);
        }
    }
}
