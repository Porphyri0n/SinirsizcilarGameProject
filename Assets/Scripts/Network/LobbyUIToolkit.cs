using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using System.Collections.Generic;

public class LobbyUIToolkit : MonoBehaviour
{
    [SerializeField] private VisualTreeAsset playerEntryTemplate;

    private UIDocument uiDocument;
    private VisualElement setupPanel;
    private VisualElement lobbyPanel;
    private TextField roomNameInput;
    private Button createRoomButton;
    private Button joinRoomButton;
    private Button quickJoinButton;
    private Label roomInfoText;
    private ScrollView playerList;
    private Button readyButton;
    private Button startGameButton;
    private Button leaveButton;

    private bool isReady = false;

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;

        setupPanel = root.Q<VisualElement>("setupPanel");
        lobbyPanel = root.Q<VisualElement>("lobbyPanel");
        roomNameInput = root.Q<TextField>("roomNameInput");
        createRoomButton = root.Q<Button>("createRoomButton");
        joinRoomButton = root.Q<Button>("joinRoomButton");
        quickJoinButton = root.Q<Button>("quickJoinButton");
        roomInfoText = root.Q<Label>("roomInfoText");
        playerList = root.Q<ScrollView>("playerList");
        readyButton = root.Q<Button>("readyButton");
        startGameButton = root.Q<Button>("startGameButton");
        leaveButton = root.Q<Button>("leaveButton");
    }

    private void Start()
    {
        createRoomButton.clicked += OnCreateRoomClicked;
        joinRoomButton.clicked += OnJoinRoomClicked;
        quickJoinButton.clicked += OnQuickJoinClicked;
        readyButton.clicked += OnReadyClicked;
        startGameButton.clicked += OnStartGameClicked;
        leaveButton.clicked += OnLeaveClicked;

        if (LobbyManager.Instance != null)
        {
            Debug.Log("[LobbyUIToolkit] Subscribing to LobbyManager events.");
            LobbyManager.Instance.OnJoinedRoomEvent += HandleJoinedRoom;
            LobbyManager.Instance.OnLobbyChanged += RefreshLobbyUI;
        }
        else
        {
            Debug.LogError("[LobbyUIToolkit] LobbyManager.Instance is null in Start! Subscription failed.");
        }

        // Host veya persistent state'ten oyunun başladığını kontrol et
        if (GameNetworkManager.Instance != null && GameNetworkManager.Instance.GameStarted)
        {
            Debug.Log("[LobbyUIToolkit] Game already started according to GameNetworkManager. Hiding UI.");
            gameObject.SetActive(false);
            return;
        }

        if (GameStateSync.Instance != null)
        {
            GameStateSync.Instance.GameStarted.OnValueChanged += OnGameStartedChanged;
            
            if (GameStateSync.Instance.GameStarted.Value)
            {
                Debug.Log("[LobbyUIToolkit] Game already started according to GameStateSync. Hiding UI.");
                gameObject.SetActive(false);
                return;
            }
        }

        ShowSetupPanel();
        
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
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
        
        if (createRoomButton != null) createRoomButton.clicked -= OnCreateRoomClicked;
        if (joinRoomButton != null) joinRoomButton.clicked -= OnJoinRoomClicked;
        if (quickJoinButton != null) quickJoinButton.clicked -= OnQuickJoinClicked;
        if (readyButton != null) readyButton.clicked -= OnReadyClicked;
        if (startGameButton != null) startGameButton.clicked -= OnStartGameClicked;
        if (leaveButton != null) leaveButton.clicked -= OnLeaveClicked;
    }

    private void Update()
    {
        // ... (Update logic remains same)
bool gameStarted = false;
        if (GameNetworkManager.Instance != null && GameNetworkManager.Instance.GameStarted)
        {
            gameStarted = true;
        }
        else if (GameStateSync.Instance != null && GameStateSync.Instance.IsSpawned && GameStateSync.Instance.GameStarted.Value)
        {
            gameStarted = true;
        }

        if (gameStarted && gameObject.activeSelf)
        {
            gameObject.SetActive(false);
            if (UnityEngine.Cursor.lockState != CursorLockMode.Locked)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
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
        setupPanel.RemoveFromClassList("hidden");
        lobbyPanel.AddToClassList("hidden");
    }

    private void HandleJoinedRoom()
    {
        Debug.Log("[LobbyUIToolkit] HandleJoinedRoom: Transitioning to Lobby Panel.");
        setupPanel.AddToClassList("hidden");
        lobbyPanel.RemoveFromClassList("hidden");
        isReady = false;
        UpdateButtonStates();
        RefreshLobbyUI();

        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
    }

    private void OnCreateRoomClicked()
    {
        string rName = string.IsNullOrEmpty(roomNameInput.value) ? "Room_" + Random.Range(1000, 9999) : roomNameInput.value;
        Debug.Log($"[LobbyUIToolkit] Create Room Clicked: {rName}");
        LobbyManager.Instance.CreateRoom(rName);
    }

    private void OnJoinRoomClicked()
    {
        if (!string.IsNullOrEmpty(roomNameInput.value))
        {
            Debug.Log($"[LobbyUIToolkit] Join Room Clicked: {roomNameInput.value}");
            if (roomNameInput.value.Length == 6)
            {
                LobbyManager.Instance.JoinByCode(roomNameInput.value);
            }
            else
            {
                LobbyManager.Instance.JoinRoom(roomNameInput.value);
            }
        }
    }

    private void OnQuickJoinClicked()
    {
        Debug.Log("[LobbyUIToolkit] Quick Join Clicked.");
        LobbyManager.Instance.QuickJoin();
    }

    private void OnReadyClicked()
    {
        isReady = !isReady;
        Debug.Log($"[LobbyUIToolkit] Ready Clicked: {isReady}");
        LobbyManager.Instance.SetReady(isReady);
        UpdateButtonStates();
    }

    private void OnStartGameClicked()
    {
        Debug.Log("[LobbyUIToolkit] Start Game Clicked.");
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.Invoke("TryStartGame", 0.1f);
        }
    }

    private void OnLeaveClicked()
    {
        Debug.Log("[LobbyUIToolkit] Leave Clicked.");
        LobbyManager.Instance.LeaveRoom();
        ShowSetupPanel();
    }

    private void UpdateButtonStates()
    {
        if (readyButton != null)
        {
            readyButton.text = isReady ? "UNREADY" : "READY";
            if (isReady)
            {
                readyButton.RemoveFromClassList("button-success");
                readyButton.AddToClassList("button-secondary");
            }
            else
            {
                readyButton.RemoveFromClassList("button-secondary");
                readyButton.AddToClassList("button-success");
            }
        }
    }

    private void RefreshLobbyUI()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            Debug.Log("[LobbyUIToolkit] RefreshLobbyUI: Not a client, showing setup panel.");
            ShowSetupPanel();
            return;
        }

        Debug.Log("[LobbyUIToolkit] Refreshing Lobby UI.");
        startGameButton.style.display = NetworkManager.Singleton.IsServer ? DisplayStyle.Flex : DisplayStyle.None;

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
            startGameButton.SetEnabled(allReady);
        }

        if (roomInfoText != null)
        {
            string joinCode = LobbyManager.Instance != null ? LobbyManager.Instance.JoinCode : "";
            roomInfoText.text = $"LOBBY: {roomNameInput.value}\nCODE: {joinCode}\nPLAYERS: {NetworkManager.Singleton.ConnectedClientsList.Count}/{GameConstants.MAX_PLAYERS_PER_ROOM}";
        }

        playerList.Clear();

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (playerEntryTemplate != null)
            {
                var entry = playerEntryTemplate.Instantiate();
                var label = entry.Q<Label>("playerName");
                if (label != null)
                {
                    string role = client.ClientId == NetworkManager.Singleton.LocalClientId ? " (You)" : "";
                    if (client.ClientId == 0) role += " [Host]";
                    bool ready = LobbyManager.Instance.IsReady(client.ClientId);
                    
                    // Note: Rich text might not work exactly same in UITK label, but let's try or use style
                    label.text = $"Player {client.ClientId}{role} - {(ready ? "READY" : "NOT READY")}";
                    if (ready) label.style.color = Color.green;
                    else label.style.color = Color.red;
                }
                playerList.Add(entry);
            }
        }
    }
}
