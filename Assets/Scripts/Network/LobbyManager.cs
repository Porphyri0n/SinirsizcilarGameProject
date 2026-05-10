using System;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

// Oda oluşturma/katılma ve lobi ready sistemi. Herkes ready olunca host oyunu başlatır.
public class LobbyManager : MonoBehaviourPunCallbacks
{
    public static LobbyManager Instance { get; private set; }

    [SerializeField] private string gameSceneName = "Game";

    public event Action OnJoinedRoomEvent;
    public event Action OnLobbyChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void CreateRoom(string roomName)
    {
        RoomOptions options = new RoomOptions { MaxPlayers = GameConstants.MAX_PLAYERS_PER_ROOM };
        PhotonNetwork.CreateRoom(roomName, options);
    }

    public void JoinRoom(string roomName) => PhotonNetwork.JoinRoom(roomName);

    public void QuickJoin() => PhotonNetwork.JoinRandomRoom();

    public void LeaveRoom() => PhotonNetwork.LeaveRoom();

    public void SetReady(bool ready)
    {
        Hashtable props = new Hashtable { { NetworkKeys.PLAYER_READY, ready } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    public bool IsReady(Player player)
    {
        return player.CustomProperties.TryGetValue(NetworkKeys.PLAYER_READY, out object value) && (bool)value;
    }

    private bool AllPlayersReady()
    {
        if (PhotonNetwork.CurrentRoom == null) return false;
        foreach (Player p in PhotonNetwork.PlayerList)
            if (!IsReady(p)) return false;
        return true;
    }

    private void TryStartGame()
    {
        if (!PhotonNetwork.IsMasterClient || !AllPlayersReady()) return;

        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.LoadLevel(gameSceneName);
    }

    // --- Photon callbacks ---

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        CreateRoom("Room_" + UnityEngine.Random.Range(1000, 9999));
    }

    public override void OnJoinedRoom()
    {
        SetReady(false);
        OnJoinedRoomEvent?.Invoke();
        OnLobbyChanged?.Invoke();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer) => OnLobbyChanged?.Invoke();

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        OnLobbyChanged?.Invoke();
        TryStartGame();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(NetworkKeys.PLAYER_READY)) return;
        OnLobbyChanged?.Invoke();
        TryStartGame();
    }

    public override void OnMasterClientSwitched(Player newMasterClient) => TryStartGame();
}
