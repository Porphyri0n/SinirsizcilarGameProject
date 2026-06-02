using System;
using UnityEngine;
using Unity.Netcode;

// Kule senkronu — giriş/çıkış, ateş ve yükseltme.
// Sahne objeleri Server sahipliğinde olduğu için RPC(SendTo.NotOwner) kullanımı yanıltıcı olabilir.
// Bunun yerine doluluk ve seviye durumunu NetworkVariable ile tutuyoruz.
public class TowerNetSync : NetworkBehaviour
{
    [SerializeField] private TowerController controller;
    [SerializeField] private TowerUpgrade upgrade;

    // NetworkVariables (Ownership hatalarını önlemek için durum senkronu)
    private readonly NetworkVariable<ulong> currentOperatorId = new NetworkVariable<ulong>(ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> currentLevel = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int CurrentLevel => currentLevel.Value;

    private int localOperatorPid = -1;
private bool isSyncingFromNetwork;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<TowerController>();
        if (upgrade == null) upgrade = GetComponent<TowerUpgrade>();
    }

    public override void OnNetworkSpawn()
    {
        currentOperatorId.OnValueChanged += OnOperatorChanged;
        currentLevel.OnValueChanged += OnLevelChanged;

        // Başlangıç değerlerini senkronize et
        if (currentOperatorId.Value != ulong.MaxValue)
            OnOperatorChanged(ulong.MaxValue, currentOperatorId.Value);
        
        if (currentLevel.Value != 0)
            OnLevelChanged(0, currentLevel.Value);
    }

    public override void OnNetworkDespawn()
    {
        currentOperatorId.OnValueChanged -= OnOperatorChanged;
        currentLevel.OnValueChanged -= OnLevelChanged;
    }

    private void OnEnable()
    {
        EventBus.OnTowerEntered += HandleTowerEntered;
        EventBus.OnTowerExited += HandleTowerExited;
        EventBus.OnTowerFired += HandleTowerFired;
        if (upgrade != null) upgrade.OnUpgraded += HandleUpgraded;
    }

    private void OnDisable()
    {
        EventBus.OnTowerEntered -= HandleTowerEntered;
        EventBus.OnTowerExited -= HandleTowerExited;
        EventBus.OnTowerFired -= HandleTowerFired;
        if (upgrade != null) upgrade.OnUpgraded -= HandleUpgraded;
    }

    // ── NetworkVariable Callbacks ───────────────────────────────────────

    private void OnOperatorChanged(ulong oldId, ulong newId)
    {
        if (isSyncingFromNetwork || controller == null) return;
        
        isSyncingFromNetwork = true;
        
        if (newId == ulong.MaxValue)
        {
            // Kule boşaldı
            if (controller.IsOccupied)
            {
                // Eğer çıkan biz değilsek controller'ı temizle (Çıkan bizsek zaten yerel Exit çağırdık)
                if (!IsLocalActor((int)oldId))
                    controller.Exit(controller.OperatorPlayer);
            }
        }
        else
        {
            // Kuleye biri girdi
            if (!IsLocalActor((int)newId))
            {
                // Eğer biz kulede olduğumuzu sanıyorsak ama sunucu başkasını diyorsa (Race condition), kuleden çık
                if (controller.IsOccupied && IsLocalActor(controller.OperatorPlayerID))
                {
                    controller.Exit(controller.OperatorPlayer);
                }

                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(newId, out var client))
                {
                    controller.SetOccupant(client.PlayerObject.gameObject, (int)newId);
                }
            }
        }
        
        isSyncingFromNetwork = false;
    }

    private void OnLevelChanged(int oldLevel, int newLevel)
    {
        if (isSyncingFromNetwork || upgrade == null) return;
        
        isSyncingFromNetwork = true;
        upgrade.SetLevel((UpgradeLevel)newLevel);
        isSyncingFromNetwork = false;
    }

    // ── Yerel kullanıcı: EventBus -> ServerRpc ─────────────────────────

    private void HandleTowerEntered(int pid, DefenseType type)
    {
        if (isSyncingFromNetwork || controller == null || !controller.IsOccupied) return;
        if (controller.OperatorPlayerID != pid || !IsLocalActor(pid)) return;

        localOperatorPid = pid;
        RequestEnterServerRpc((ulong)pid);
    }

    private void HandleTowerExited(int pid, DefenseType type)
    {
        if (isSyncingFromNetwork || pid != localOperatorPid) return;

        localOperatorPid = -1;
        RequestExitServerRpc();
    }

    private void HandleTowerFired(DefenseType type, Vector3 target)
    {
        if (controller == null || !controller.IsOccupied) return;
        if (!IsLocalActor(controller.OperatorPlayerID)) return;

        // Görsel efekt RPC'si Unreliable (hızlı ve hafif)
        RPC_TowerFireRpc((int)type, target);
    }

    private void HandleUpgraded(UpgradeLevel level)
    {
        if (isSyncingFromNetwork || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient) return;
        RequestUpgradeServerRpc((int)level);
    }

    // ── ServerRpc'ler (State changes) ───────────────────────────────────

    [ServerRpc(RequireOwnership = false)]
    private void RequestEnterServerRpc(ulong clientId)
    {
        if (currentOperatorId.Value == ulong.MaxValue)
        {
            currentOperatorId.Value = clientId;
        }
        else
        {
            Debug.LogWarning($"Client {clientId} tried to enter a tower already occupied by {currentOperatorId.Value}");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestExitServerRpc()
    {
        currentOperatorId.Value = ulong.MaxValue;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestUpgradeServerRpc(int levelInt)
    {
        currentLevel.Value = levelInt;
    }

    // ── Rpc'ler (Visuals) ───────────────────────────────────────────────

    // Ateş görselini diğerlerine gönderiyoruz.
    // Delivery = Unreliable kullanılarak performans artırılır.
    [Rpc(SendTo.NotOwner, Delivery = RpcDelivery.Unreliable)]
    private void RPC_TowerFireRpc(int typeInt, Vector3 target)
    {
        EventBus.FireTowerFired((DefenseType)typeInt, target);
    }

    // ── Yardımcı ────────────────────────────────────────────────────────

    private static bool IsLocalActor(int pid)
    {
        return NetworkManager.Singleton != null
            && NetworkManager.Singleton.IsClient
            && (ulong)pid == NetworkManager.Singleton.LocalClientId;
    }
}
