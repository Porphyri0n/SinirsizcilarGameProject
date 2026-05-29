using System;
using UnityEngine;
using Unity.Netcode;

// Kule senkronu — giriş/çıkış, ateş ve yükseltme RPC'leri. Kule sabit olduğu için pozisyon stream'i yok.
// Kuleyi yerel kullanan client EventBus olaylarını RpcTarget.Others'a taşır; diğer client'lar gelen
// RPC'de aynı EventBus olayını tekrar fire eder (UI/efekt/mermi görseli OnTowerFired dinleyicilerinde olur).
// Yükseltme TowerUpgrade'in instance event'inden alınır; alıcıda guard ile tekrar yayınlanmaz.
public class TowerNetSync : NetworkBehaviour
{
    [SerializeField] private TowerController controller;
    [SerializeField] private TowerUpgrade upgrade;

    private int localOperatorPid = -1;      // bu client'ın bu kuleyi kullandığı pid (giriş↔çıkış eşlemesi)
    private bool applyingRemoteUpgrade;     // RPC ile gelen yükseltmeyi tekrar yayınlamamak için

    private void Awake()
    {
        if (controller == null) controller = GetComponent<TowerController>();
        if (upgrade == null) upgrade = GetComponent<TowerUpgrade>();
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

    // ── Yerel kullanıcı: EventBus -> RPC ────────────────────────────────

    // Sadece bu kuleyi yerel oyuncu kullanıyorsa yayınla (aynı tipte başka kule yanlış tetiklenmesin).
    private void HandleTowerEntered(int pid, DefenseType type)
    {
        if (controller == null || !controller.IsOccupied) return;
        if (controller.OperatorPlayerID != pid || !IsLocalActor(pid)) return;

        localOperatorPid = pid;
        RPC_EnterTowerRpc(pid, (int)type);
    }

    private void HandleTowerExited(int pid, DefenseType type)
    {
        if (pid != localOperatorPid) return;    // bizim girişimizin çıkışı değil

        localOperatorPid = -1;
        RPC_ExitTowerRpc(pid, (int)type);
    }

    private void HandleTowerFired(DefenseType type, Vector3 target)
    {
        if (controller == null || !controller.IsOccupied) return;
        if (!IsLocalActor(controller.OperatorPlayerID)) return;

        RPC_TowerFireRpc((int)type, target);
    }

    private void HandleUpgraded(UpgradeLevel level)
    {
        if (applyingRemoteUpgrade || Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsClient) return;
        RPC_UpgradeRpc((int)level);
    }

    // ── RPC'ler (alıcı tarafı — yerel EventBus'a yeniden yayınla) ────────

    [Rpc(SendTo.NotOwner)]
    private void RPC_EnterTowerRpc(int pid, int typeInt)
    {
        EventBus.FireTowerEntered(pid, (DefenseType)typeInt);
    }

    [Rpc(SendTo.NotOwner)]
    private void RPC_ExitTowerRpc(int pid, int typeInt)
    {
        EventBus.FireTowerExited(pid, (DefenseType)typeInt);
    }

    // Ateş herkeste görünsün — mermi/namlu görseli OnTowerFired dinleyicilerinde spawn olur.
    [Rpc(SendTo.NotOwner)]
    private void RPC_TowerFireRpc(int typeInt, Vector3 target)
    {
        EventBus.FireTowerFired((DefenseType)typeInt, target);
    }

    // Yükseltmeyi alıcıda da uygula → tier/stat senkron kalır, yerel EventBus.FireUpgradeCompleted tetiklenir.
    [Rpc(SendTo.NotOwner)]
    private void RPC_UpgradeRpc(int levelInt)
    {
        if (upgrade == null) return;

        applyingRemoteUpgrade = true;
        if (upgrade.CurrentLevel != (UpgradeLevel)levelInt && upgrade.CanUpgrade())
            upgrade.Upgrade();
        applyingRemoteUpgrade = false;
    }

    // ── Yardımcı ────────────────────────────────────────────────────────

    private static bool IsLocalActor(int pid)
    {
        return Unity.Netcode.NetworkManager.Singleton != null
            && Unity.Netcode.NetworkManager.Singleton.IsClient
            && (ulong)pid == Unity.Netcode.NetworkManager.Singleton.LocalClientId;
    }
}
