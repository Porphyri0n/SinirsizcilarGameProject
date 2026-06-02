using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Services.Multiplayer;

// Oda oluşturma/katılma ve lobi ready sistemi. Herkes ready olunca host oyunu başlatır.
// Yeni com.unity.services.multiplayer SDK'sını kullanır.
public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [SerializeField] private string gameSceneName = "GameScene";

    public event Action OnJoinedRoomEvent;
    public event Action OnLobbyChanged;

    private ISession currentSession;
    private string currentSessionId;

    public string JoinCode => currentSession?.Code ?? "";

    // Server-side ready tracker
    private readonly Dictionary<ulong, bool> playerReadyStates = new Dictionary<ulong, bool>();
    // Client-side ready tracker (synchronized from server)
    private readonly Dictionary<ulong, bool> clientReadyStates = new Dictionary<ulong, bool>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public async void CreateRoom(string roomName)
    {
        try
        {
            int maxPlayers = GameConstants.MAX_PLAYERS_PER_ROOM;
            var options = new SessionOptions
            {
                MaxPlayers = maxPlayers,
                IsPrivate = false,
                Name = roomName
            }.WithRelayNetwork(); // Relay bağlantısını otomatik ayarlar

            currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
            currentSessionId = currentSession.Id;

            Debug.Log($"[LobbyManager] Created session '{roomName}' with Join Code: {currentSession.Code}");
            
            // Start Netcode as Host
            NetworkManager.Singleton.StartHost();
            
            OnJoinedRoomEvent?.Invoke();
OnLobbyChanged?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyManager] Failed to create session: {e.Message}");
        }
    }

    public async void JoinRoom(string roomName)
    {
        try
        {
            // Aktif session'ları sorgula ve eşleşen ada sahip olana bağlan
            var queryOptions = new QuerySessionsOptions { Count = 20 };
            var queryResponse = await MultiplayerService.Instance.QuerySessionsAsync(queryOptions);

            ISessionInfo targetSession = null;
            foreach (var session in queryResponse.Sessions)
            {
                if (session.Name == roomName)
                {
                    targetSession = session;
                    break;
                }
            }

            if (targetSession != null)
            {
                currentSession = await MultiplayerService.Instance.JoinSessionByIdAsync(targetSession.Id);
                currentSessionId = currentSession.Id;
                
                Debug.Log($"[LobbyManager] Joined session '{roomName}' with ID: {currentSessionId}");
                
                // Start Netcode as Client
                NetworkManager.Singleton.StartClient();
                
                OnJoinedRoomEvent?.Invoke();
                OnLobbyChanged?.Invoke();
            }
            else
            {
                Debug.LogError($"[LobbyManager] Session not found with name: {roomName}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyManager] Failed to join session: {e.Message}");
        }
    }

    public async void JoinByCode(string code)
    {
        try
        {
            currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
            currentSessionId = currentSession.Id;

            Debug.Log($"[LobbyManager] Joined session by code '{code}' with ID: {currentSessionId}");

            // Start Netcode as Client
            NetworkManager.Singleton.StartClient();

            OnJoinedRoomEvent?.Invoke();
            OnLobbyChanged?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyManager] Failed to join session by code: {e.Message}");
        }
    }

    public async void QuickJoin()
    {
        try
        {
            var queryOptions = new QuerySessionsOptions { Count = 10 };
            var queryResponse = await MultiplayerService.Instance.QuerySessionsAsync(queryOptions);

            if (queryResponse.Sessions.Count > 0)
            {
                var session = queryResponse.Sessions[0];
                currentSession = await MultiplayerService.Instance.JoinSessionByIdAsync(session.Id);
                currentSessionId = currentSession.Id;
                
                Debug.Log($"[LobbyManager] Quick joined session with ID: {currentSessionId}");
                
                // Start Netcode as Client
                NetworkManager.Singleton.StartClient();
                
                OnJoinedRoomEvent?.Invoke();
OnLobbyChanged?.Invoke();
            }
            else
            {
                Debug.LogWarning("[LobbyManager] No active session found for QuickJoin. Creating one.");
                CreateRoom("Room_" + UnityEngine.Random.Range(1000, 9999));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LobbyManager] Quick join failed: {e.Message}. Creating a new room.");
            CreateRoom("Room_" + UnityEngine.Random.Range(1000, 9999));
        }
    }

    public async void LeaveRoom()
    {
        try
        {
            if (currentSession != null)
            {
                await currentSession.LeaveAsync();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LobbyManager] Error leaving session: {e.Message}");
        }

        currentSession = null;
        currentSessionId = null;

        OnLobbyChanged?.Invoke();
    }

    public void SetReady(bool ready)
    {
        if (IsSpawned && IsClient)
        {
            SetReadyServerRpc(Unity.Netcode.NetworkManager.Singleton.LocalClientId, ready);
        }
        else
        {
            Debug.LogWarning("[LobbyManager] Cannot set ready: Network object not spawned or not a client.");
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SetReadyServerRpc(ulong clientId, bool ready)
    {
        playerReadyStates[clientId] = ready;
        UpdateClientReadyStatesClientRpc(clientId, ready);
        TryStartGame();
    }

    [ClientRpc]
    private void UpdateClientReadyStatesClientRpc(ulong clientId, bool ready)
    {
        clientReadyStates[clientId] = ready;
        OnLobbyChanged?.Invoke();
    }

    [ClientRpc]
    private void RemoveClientReadyStateClientRpc(ulong clientId)
    {
        clientReadyStates.Remove(clientId);
        OnLobbyChanged?.Invoke();
    }

    public bool IsReady(ulong clientId)
    {
        return clientReadyStates.TryGetValue(clientId, out bool ready) && ready;
    }

    public bool IsReady(object player) => false;

    private bool AllPlayersReady()
    {
        if (Unity.Netcode.NetworkManager.Singleton == null) return false;
        foreach (var client in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList)
        {
            if (!playerReadyStates.TryGetValue(client.ClientId, out bool ready) || !ready)
                return false;
        }
        return true;
    }

    private void TryStartGame()
    {
        if (!IsServer || !AllPlayersReady()) return;

        // Tüm clientlara oyunun başladığını haber ver (Lobby UI'larını kapatsınlar)
        SetGameStartedClientRpc();

        if (GameNetworkManager.Instance != null)
        {
            GameNetworkManager.Instance.GameStarted = true;
        }

        // Senkronizasyon için GameStateSync üzerindeki NetworkVariable'ı güncelle
        if (GameStateSync.Instance != null)
        {
            GameStateSync.Instance.GameStarted.Value = true;
        }

        CloseSession();
        Unity.Netcode.NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    [ClientRpc]
    private void SetGameStartedClientRpc()
    {
        if (GameNetworkManager.Instance != null)
        {
            GameNetworkManager.Instance.GameStarted = true;
        }
    }

    private async void CloseSession()
    {
        try
        {
            if (currentSession != null)
            {
                // Yeni oyuncuların girmesini önlemek için kilitle
                // multiplayer SDK session güncellemesi (host kilidi)
                // ISession nesnesi üzerinden session ayarlarından kilitlenebilir.
            }
            await Task.Yield();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LobbyManager] Failed to close session: {e.Message}");
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Unity.Netcode.NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            Unity.Netcode.NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
            playerReadyStates[Unity.Netcode.NetworkManager.Singleton.LocalClientId] = false;
        }
        OnLobbyChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && Unity.Netcode.NetworkManager.Singleton != null)
        {
            Unity.Netcode.NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            Unity.Netcode.NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        playerReadyStates[clientId] = false;
        UpdateClientReadyStatesClientRpc(clientId, false);

        // Sync all existing ready states to the newly joined client
        foreach (var pair in playerReadyStates)
        {
            UpdateClientReadyStatesClientRpc(pair.Key, pair.Value);
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        playerReadyStates.Remove(clientId);
        RemoveClientReadyStateClientRpc(clientId);
        TryStartGame();
    }
}
