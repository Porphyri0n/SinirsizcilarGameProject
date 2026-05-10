using System;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Photon PUN 2 bağlantı yönetimi — Singleton.
/// Master server'a bağlanır, region ayarını yapar ve bağlantı callback'lerini yönetir.
/// Doğrudan EventBus event'i fire etmez; bağlantı durumunu kendi event'leri ile bildirir.
/// </summary>
public class NetworkManager : MonoBehaviourPunCallbacks
{
    public static NetworkManager Instance { get; private set; }

    [Header("Photon Ayarları")]
    [SerializeField] private string gameVersion = "1.0";
    [SerializeField] private string fixedRegion = "eu";        // Boş bırakılırsa Photon en iyi region'u seçer
    [SerializeField] private bool connectOnStart = true;

    /// <summary>Aynı odadaki maksimum oyuncu sayısı (GameConstants'tan).</summary>
    public byte MaxPlayersPerRoom => GameConstants.MAX_PLAYERS_PER_ROOM;

    public bool IsConnectedToMaster { get; private set; }

    /// <summary>Master server'a bağlanıldığında tetiklenir.</summary>
    public event Action OnConnectedToMasterServer;
    /// <summary>Sunucu bağlantısı koptuğunda tetiklenir.</summary>
    public event Action<DisconnectCause> OnDisconnectedFromServer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Host (MasterClient) sahne geçişlerini yönetir, client'lar otomatik takip eder.
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = gameVersion;
    }

    private void Start()
    {
        if (connectOnStart)
            ConnectToServer();
    }

    /// <summary>Photon master server'a bağlanır. Zaten bağlıysa hiçbir şey yapmaz.</summary>
    public void ConnectToServer()
    {
        if (PhotonNetwork.IsConnected)
            return;

        if (!string.IsNullOrEmpty(fixedRegion))
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = fixedRegion;

        PhotonNetwork.ConnectUsingSettings();
    }

    /// <summary>Sunucu bağlantısını kapatır.</summary>
    public void DisconnectFromServer()
    {
        if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();
    }

    // ── Photon Bağlantı Callback'leri ───────────────────────────────────

    public override void OnConnectedToMaster()
    {
        IsConnectedToMaster = true;
        Debug.Log($"[NetworkManager] Master server'a bağlanıldı (region: {PhotonNetwork.CloudRegion}).");
        OnConnectedToMasterServer?.Invoke();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        IsConnectedToMaster = false;
        Debug.LogWarning($"[NetworkManager] Sunucu bağlantısı koptu: {cause}");
        OnDisconnectedFromServer?.Invoke(cause);
    }
}
