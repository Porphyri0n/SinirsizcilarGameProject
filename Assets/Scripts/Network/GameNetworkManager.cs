using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Services.Core;
using Unity.Services.Authentication;

public enum DisconnectCause
{
    None,
    Exception,
    ServerDown,
    ClientDisconnect,
    Timeout
}

/// <summary>
/// Unity Netcode & Services bağlantı yönetimi — Singleton.
/// Unity Services ve Authentication'ı başlatır, bağlantı durumlarını yönetir.
/// </summary>
public class GameNetworkManager : MonoBehaviour
{
    public static GameNetworkManager Instance { get; private set; }

    [Header("Ayarlar")]
    [SerializeField] private bool connectOnStart = true;
    [SerializeField] private bool autoReconnect = true;          // beklenmedik kopmada otomatik yeniden bağlan
    [SerializeField] private int maxReconnectAttempts = 3;
    [SerializeField] private float reconnectDelay = 2f;          // her deneme arası bekleme (sn)

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
    private bool isInitializing;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        if (connectOnStart)
            await ConnectToServerAsync();
    }

    /// <summary>Unity Services & Authentication master bağlantısını gerçekleştirir.</summary>
    public async void ConnectToServer()
    {
        await ConnectToServerAsync();
    }

    public async Task ConnectToServerAsync()
    {
        if (IsConnectedToMaster || isInitializing)
            return;

        isInitializing = true;
        reconnectAttempts = 0;
        intentionalDisconnect = false;

        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            IsConnectedToMaster = true;
            Debug.Log($"[GameNetworkManager] Unity Services Başlatıldı. Oyuncu ID: {AuthenticationService.Instance.PlayerId}");
            OnConnectedToMasterServer?.Invoke();

            if (Unity.Netcode.NetworkManager.Singleton != null)
            {
                Unity.Netcode.NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameNetworkManager] Bağlantı Hatası: {ex.Message}");
            IsConnectedToMaster = false;
            OnDisconnectedFromServer?.Invoke(DisconnectCause.Exception);
            HandleReconnect();
        }
        finally
        {
            isInitializing = false;
        }
    }

    /// <summary>Sunucu bağlantısını kapatır.</summary>
    public void DisconnectFromServer()
    {
        intentionalDisconnect = true;
        IsConnectedToMaster = false;

        if (Unity.Netcode.NetworkManager.Singleton != null)
        {
            Unity.Netcode.NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
            if (Unity.Netcode.NetworkManager.Singleton.IsClient || Unity.Netcode.NetworkManager.Singleton.IsServer)
            {
                Unity.Netcode.NetworkManager.Singleton.Shutdown();
            }
        }
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        if (Unity.Netcode.NetworkManager.Singleton != null && clientId == Unity.Netcode.NetworkManager.Singleton.LocalClientId)
        {
            IsConnectedToMaster = false;
            Debug.LogWarning("[GameNetworkManager] Netcode bağlantısı kapandı.");
            OnDisconnectedFromServer?.Invoke(DisconnectCause.ClientDisconnect);

            if (!intentionalDisconnect && autoReconnect)
            {
                HandleReconnect();
            }
        }
    }

    private void HandleReconnect()
    {
        if (reconnectAttempts >= maxReconnectAttempts)
        {
            reconnectAttempts = 0;
            Debug.LogWarning("[GameNetworkManager] Yeniden bağlanma başarısız — denemeler tükendi.");
            OnReconnectFailed?.Invoke();
            return;
        }

        reconnectAttempts++;
        OnReconnecting?.Invoke(reconnectAttempts);
        Invoke(nameof(AttemptReconnect), reconnectDelay);
    }

    private async void AttemptReconnect()
    {
        await ConnectToServerAsync();
    }
}
