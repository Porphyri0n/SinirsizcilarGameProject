using System;
using UnityEngine;
using Photon.Pun;

// Kule senkronu — giriş/çıkış, ateş ve yükseltme RPC'leri. Kule sabit olduğu için pozisyon stream'i yok.
// Kuleyi yerel kullanan client EventBus olaylarını RpcTarget.Others'a taşır; diğer client'lar gelen
// RPC'de aynı EventBus olayını tekrar fire eder (UI/efekt/mermi görseli OnTowerFired dinleyicilerinde olur).
// Yükseltme TowerUpgrade'in instance event'inden alınır; alıcıda guard ile tekrar yayınlanmaz.
[RequireComponent(typeof(PhotonView))]
public class TowerNetSync : MonoBehaviourPun
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
        photonView.RPC(NetworkKeys.RPC_ENTER_TOWER, RpcTarget.Others, pid, (int)type);
    }

    private void HandleTowerExited(int pid, DefenseType type)
    {
        if (pid != localOperatorPid) return;    // bizim girişimizin çıkışı değil

        localOperatorPid = -1;
        photonView.RPC(NetworkKeys.RPC_EXIT_TOWER, RpcTarget.Others, pid, (int)type);
    }

    private void HandleTowerFired(DefenseType type, Vector3 target)
    {
        if (controller == null || !controller.IsOccupied) return;
        if (!IsLocalActor(controller.OperatorPlayerID)) return;

        photonView.RPC(NetworkKeys.RPC_TOWER_FIRE, RpcTarget.Others, (int)type, target);
    }

    private void HandleUpgraded(UpgradeLevel level)
    {
        if (applyingRemoteUpgrade || !PhotonNetwork.InRoom) return;
        photonView.RPC(NetworkKeys.RPC_UPGRADE, RpcTarget.Others, (int)level);
    }

    // ── RPC'ler (alıcı tarafı — yerel EventBus'a yeniden yayınla) ────────

    [PunRPC]
    private void RPC_EnterTower(int pid, int typeInt)
    {
        EventBus.FireTowerEntered(pid, (DefenseType)typeInt);
    }

    [PunRPC]
    private void RPC_ExitTower(int pid, int typeInt)
    {
        EventBus.FireTowerExited(pid, (DefenseType)typeInt);
    }

    // Ateş herkeste görünsün — mermi/namlu görseli OnTowerFired dinleyicilerinde spawn olur.
    [PunRPC]
    private void RPC_TowerFire(int typeInt, Vector3 target)
    {
        EventBus.FireTowerFired((DefenseType)typeInt, target);
    }

    // Yükseltmeyi alıcıda da uygula → tier/stat senkron kalır, yerel EventBus.FireUpgradeCompleted tetiklenir.
    [PunRPC]
    private void RPC_Upgrade(int levelInt)
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
        return PhotonNetwork.InRoom
            && PhotonNetwork.LocalPlayer != null
            && pid == PhotonNetwork.LocalPlayer.ActorNumber;
    }
}
