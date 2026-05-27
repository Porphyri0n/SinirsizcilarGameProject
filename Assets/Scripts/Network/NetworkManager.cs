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

    [Header("Reconnect")]
    [SerializeField] private bool autoReconnect = true;          // beklenmedik kopmada otomatik yeniden bağlan
    [SerializeField] private int maxReconnectAttempts = 3;
    [SerializeField] private float reconnectDelay = 2f;          // her deneme arası bekleme (sn)

    [Header("Sync Hızı")]
    [SerializeField] private int sendRateMultiplier = 2;         // SendRate = serializationRate × bu (RPC/flush daha sık)

    /// <summary>Aynı odadaki maksimum oyuncu sayısı (GameConstants'tan).</summary>
    public byte MaxPlayersPerRoom => GameConstants.MAX_PLAYERS_PER_ROOM;

    public bool IsConnectedToMaster { get; private set; }

    /// <summary>Master server'a bağlanıldığında tetiklenir.</summary>
    public event Action OnConnectedToMasterServer;
    /// <summary>Sunucu bağlantısı koptuğunda tetiklenir.</summary>
    public event Action<DisconnectCause> OnDisconnectedFromServer;
    /// <summary>Yeniden bağlanma denemesi başladığında (kaçıncı deneme) tetiklenir.</summary>
    public event Action<int> OnReconnecting;
    /// <summary>Tüm yeniden bağlanma denemeleri tükendiğinde tetiklenir.</summary>
    public event Action OnReconnectFailed;

    private int reconnectAttempts;
    private bool intentionalDisconnect;   // DisconnectFromServer çağrıldıysa reconnect denenmez

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
        ConfigureNetworkRates();
    }

    // Serileştirme ve gönderim hızını NETWORK_SYNC_RATE'ten merkezi olarak ayarlar.
    // SerializationRate transform stream'ini sürer (~10 Hz); SendRate flush/RPC sıklığıdır,
    // RPC'ler gecikmesin diye serileştirmenin katı tutulur. Eskiden bu global ayar her oyuncu
    // spawn'ında PlayerNetSync.Awake'te tekrar yapılıyordu — tek yere taşındı.
    private void ConfigureNetworkRates()
    {
        int serializationRate = Mathf.Max(1, Mathf.RoundToInt(1f / GameConstants.NETWORK_SYNC_RATE));
        PhotonNetwork.SerializationRate = serializationRate;
        PhotonNetwork.SendRate = serializationRate * Mathf.Max(1, sendRateMultiplier);
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

        reconnectAttempts = 0;
        intentionalDisconnect = false;

        if (!string.IsNullOrEmpty(fixedRegion))
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = fixedRegion;

        PhotonNetwork.ConnectUsingSettings();
    }

    /// <summary>Sunucu bağlantısını kapatır (kullanıcı isteğiyle — reconnect denenmez).</summary>
    public void DisconnectFromServer()
    {
        if (PhotonNetwork.IsConnected)
        {
            intentionalDisconnect = true;
            PhotonNetwork.Disconnect();
        }
    }

    // ── Photon Bağlantı Callback'leri ───────────────────────────────────

    public override void OnConnectedToMaster()
    {
        IsConnectedToMaster = true;
        reconnectAttempts = 0;
        Debug.Log($"[NetworkManager] Master server'a bağlanıldı (region: {PhotonNetwork.CloudRegion}).");
        OnConnectedToMasterServer?.Invoke();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        IsConnectedToMaster = false;
        Debug.LogWarning($"[NetworkManager] Sunucu bağlantısı koptu: {cause}");
        OnDisconnectedFromServer?.Invoke(cause);

        // Kullanıcı bilerek çıktıysa veya otomatik reconnect kapalıysa deneme yapma
        if (intentionalDisconnect || !autoReconnect)
        {
            intentionalDisconnect = false;
            return;
        }

        if (reconnectAttempts >= maxReconnectAttempts)
        {
            reconnectAttempts = 0;
            Debug.LogWarning("[NetworkManager] Yeniden bağlanma başarısız — denemeler tükendi.");
            OnReconnectFailed?.Invoke();
            return;
        }

        reconnectAttempts++;
        OnReconnecting?.Invoke(reconnectAttempts);
        Invoke(nameof(AttemptReconnect), reconnectDelay);
    }

    // Oda hâlâ duruyorsa odaya geri dön (oyun ortası rejoin), yoksa master'a yeniden bağlan.
    private void AttemptReconnect()
    {
        if (!PhotonNetwork.ReconnectAndRejoin())
            PhotonNetwork.Reconnect();
    }

    // Odaya (yeniden) katılınca deneme sayacı sıfırlanır.
    public override void OnJoinedRoom()
    {
        reconnectAttempts = 0;
    }
}
