using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class LobbyUI : MonoBehaviour
{
    [Header("Setup Panel UI")]
    [SerializeField] private GameObject setupPanel;
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button quickJoinButton;

    [Header("Lobby Panel UI")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private TMP_Text roomInfoText;
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private GameObject playerEntryTemplate; // Template inside playerListContainer to clone
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveButton;

    private bool isReady = false;

    private void Start()
    {
        createRoomButton.onClick.AddListener(OnCreateRoomClicked);
        joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
        quickJoinButton.onClick.AddListener(OnQuickJoinClicked);
        readyButton.onClick.AddListener(OnReadyClicked);
        startGameButton.onClick.AddListener(OnStartGameClicked);
        leaveButton.onClick.AddListener(OnLeaveClicked);

        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnJoinedRoomEvent += HandleJoinedRoom;
            LobbyManager.Instance.OnLobbyChanged += RefreshLobbyUI;
        }

        // Hide player template
        if (playerEntryTemplate != null)
        {
            playerEntryTemplate.SetActive(false);
        }

        // Host veya persistent state'ten oyunun başladığını kontrol et
        if (GameNetworkManager.Instance != null && GameNetworkManager.Instance.GameStarted)
        {
            gameObject.SetActive(false);
            return;
        }

        // Check if game has already started (for late joiners or after scene reloads)
        if (GameStateSync.Instance != null && GameStateSync.Instance.GameStarted.Value)
        {
            gameObject.SetActive(false);
            return;
        }

        if (GameStateSync.Instance != null)
        {
            GameStateSync.Instance.GameStarted.OnValueChanged += OnGameStartedChanged;
        }

        ShowSetupPanel();
        
        // Lobby açıldığında cursor'ı serbest bırak
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnJoinedRoomEvent -= HandleJoinedRoom;
            LobbyManager.Instance.OnLobbyChanged -= RefreshLobbyUI;
        }

        if (GameStateSync.Instance != null)
        {
            GameStateSync.Instance.GameStarted.OnValueChanged -= OnGameStartedChanged;
        }
    }

    private void Update()
    {
        // Oyunun başlayıp başlamadığını kontrol et
        bool gameStarted = false;
        if (GameNetworkManager.Instance != null && GameNetworkManager.Instance.GameStarted)
        {
            gameStarted = true;
        }
        else if (GameStateSync.Instance != null && GameStateSync.Instance.IsSpawned && GameStateSync.Instance.GameStarted.Value)
        {
            gameStarted = true;
        }

        // Safety check: Oyun başladıysa ama UI hala açıksa kapat
        if (gameStarted && gameObject.activeSelf)
        {
            gameObject.SetActive(false);
            
            // UI kapandığında cursor'ı oyun için hazırla
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private void OnGameStartedChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            gameObject.SetActive(false);
        }
    }

    private void ShowSetupPanel()
    {
        setupPanel.SetActive(true);
        lobbyPanel.SetActive(false);
    }

    private void HandleJoinedRoom()
    {
        setupPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        isReady = false;
        UpdateButtonStates();
        RefreshLobbyUI();

        // Ensure cursor is free in the lobby
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnCreateRoomClicked()
    {
        string rName = string.IsNullOrEmpty(roomNameInput.text) ? "Room_" + Random.Range(1000, 9999) : roomNameInput.text;
        LobbyManager.Instance.CreateRoom(rName);
    }

    private void OnJoinRoomClicked()
    {
        if (!string.IsNullOrEmpty(roomNameInput.text))
        {
            LobbyManager.Instance.JoinRoom(roomNameInput.text);
        }
    }

    private void OnQuickJoinClicked()
    {
        LobbyManager.Instance.QuickJoin();
    }

    private void OnReadyClicked()
    {
        isReady = !isReady;
        LobbyManager.Instance.SetReady(isReady);
        UpdateButtonStates();
    }

    private void OnStartGameClicked()
    {
        // Host starts the game
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.Invoke("TryStartGame", 0.1f);
        }
    }

    private void OnLeaveClicked()
    {
        LobbyManager.Instance.LeaveRoom();
        ShowSetupPanel();
    }

    private void UpdateButtonStates()
    {
        if (readyButtonText != null)
        {
            readyButtonText.text = isReady ? "UNREADY" : "READY";
        }
    }

    private void RefreshLobbyUI()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            ShowSetupPanel();
            return;
        }

        // Show start game button only to Host
        startGameButton.gameObject.SetActive(NetworkManager.Singleton.IsServer);

        // Enable start game button if all players are ready
        if (NetworkManager.Singleton.IsServer)
        {
            bool allReady = true;
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (!LobbyManager.Instance.IsReady(client.ClientId))
                {
                    allReady = false;
                    break;
                }
            }
            startGameButton.interactable = allReady;
        }

        // Display room name/info
        if (roomInfoText != null)
        {
            roomInfoText.text = $"LOBBY: {roomNameInput.text}\nPLAYERS: {NetworkManager.Singleton.ConnectedClientsList.Count}/{GameConstants.MAX_PLAYERS_PER_ROOM}";
        }

        // Clear existing entries in container (excluding template)
        foreach (Transform child in playerListContainer)
        {
            if (child.gameObject != playerEntryTemplate)
            {
                Destroy(child.gameObject);
            }
        }

        // Rebuild list of players
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (playerEntryTemplate != null)
            {
                GameObject entry = Instantiate(playerEntryTemplate, playerListContainer);
                entry.SetActive(true);
                TMP_Text text = entry.GetComponentInChildren<TMP_Text>();
                if (text != null)
                {
                    string role = client.ClientId == NetworkManager.Singleton.LocalClientId ? " (You)" : "";
                    if (client.ClientId == 0) role += " [Host]";
                    bool ready = LobbyManager.Instance.IsReady(client.ClientId);
                    text.text = $"Player {client.ClientId}{role} - {(ready ? "<color=green>READY</color>" : "<color=red>NOT READY</color>")}";
                }
            }
        }
    }
}
