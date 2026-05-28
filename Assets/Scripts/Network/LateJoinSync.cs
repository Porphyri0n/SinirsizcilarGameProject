using System;
using UnityEngine;
using Photon.Pun;
using ExitGames.Client.Photon;

// GameStateSync faz/dalga/kale HP'sini senkronlar. LateJoinSync onun yanina
// devam eden oyuna katilan oyuncunun kacirdigi kalici state'i ekler:
//   - Kale deposundaki kaynak stoklari (her ResourceType icin)
//   - Game over bayragi (survivedWaves ile)
// Host EventBus event'lerinden okuyup oda custom property'lerine yazar;
// geç katılan client OnJoinedRoom'da property'leri okuyup local EventBus'a
// fire eder, boylece UI / EconomyManager kendi state'ini kurar.
public class LateJoinSync : MonoBehaviourPunCallbacks
{
    // Oda property anahtarlari — bu dosyaya ozel, NetworkKeys'i kirletmemek icin local.
    private const string ROOM_RESOURCES = "lateRes";   // int[] (type, amount) ciftleri
    private const string ROOM_GAME_OVER = "lateGO";    // int — survivedWaves; yoksa oyun bitmemis

    public override void OnEnable()
    {
        base.OnEnable();
        EventBus.OnResourceReceived += HandleResourceChange;
        EventBus.OnResourceDeposited += HandleResourceChange;
        EventBus.OnGameLost += HandleGameLost;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        EventBus.OnResourceReceived -= HandleResourceChange;
        EventBus.OnResourceDeposited -= HandleResourceChange;
        EventBus.OnGameLost -= HandleGameLost;
    }

    // ── Host: state -> room property ────────────────────────────────────

    private void HandleResourceChange(ResourceType type, int amount)
    {
        if (!CanBroadcast()) return;
        BroadcastResources();
    }

    private void HandleGameLost(int survivedWaves)
    {
        if (!CanBroadcast()) return;
        SetRoomProperty(ROOM_GAME_OVER, survivedWaves);
    }

    private void BroadcastResources()
    {
        if (EconomyManager.Instance == null || PhotonNetwork.CurrentRoom == null) return;

        // Sadece pozitif stoklari topla; (type, amount) cift array'i
        var values = (ResourceType[])Enum.GetValues(typeof(ResourceType));
        int count = 0;
        for (int i = 0; i < values.Length; i++)
            if (EconomyManager.Instance.GetStock(values[i]) > 0) count++;

        int[] encoded = new int[count * 2];
        int idx = 0;
        for (int i = 0; i < values.Length; i++)
        {
            int stock = EconomyManager.Instance.GetStock(values[i]);
            if (stock <= 0) continue;
            encoded[idx++] = (int)values[i];
            encoded[idx++] = stock;
        }

        SetRoomProperty(ROOM_RESOURCES, encoded);
    }

    // ── Late joiner: room property -> EventBus ──────────────────────────

    public override void OnJoinedRoom()
    {
        // Host ise zaten kendi state'i — sadece geç katılanlar için
        if (AuthorityManager.IsHost) return;
        ApplyLateJoinState();
    }

    private void ApplyLateJoinState()
    {
        if (PhotonNetwork.CurrentRoom == null) return;
        Hashtable props = PhotonNetwork.CurrentRoom.CustomProperties;

        if (props.TryGetValue(ROOM_RESOURCES, out object raw) && raw is int[] encoded)
        {
            for (int i = 0; i + 1 < encoded.Length; i += 2)
            {
                ResourceType type = (ResourceType)encoded[i];
                int amount = encoded[i + 1];
                if (amount > 0)
                    EventBus.FireResourceReceived(type, amount);
            }
        }

        if (props.TryGetValue(ROOM_GAME_OVER, out object gameOver) && gameOver is int survived)
            EventBus.FireGameLost(survived);
    }

    // ── Yardimcilar ─────────────────────────────────────────────────────

    private static bool CanBroadcast() => PhotonNetwork.InRoom && AuthorityManager.IsHost;

    private static void SetRoomProperty(string key, object value)
    {
        if (PhotonNetwork.CurrentRoom == null) return;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { key, value } });
    }
}
