using System;
using UnityEngine;
using Photon.Pun;

// Host (MasterClient) otorite kontrolleri.
// Host: wave spawn, phase geçiş, kervan spawn, loot dağıtım, haydut spawn.
// Client: sadece input gönderir ve kendi player'ını kontrol eder.
public static class AuthorityManager
{
    public static bool IsHost => PhotonNetwork.IsMasterClient;

    public static bool CanSpawnWave() => IsHost;
    public static bool CanChangePhase() => IsHost;
    public static bool CanSpawnCaravan() => IsHost;
    public static bool CanSpawnBandits() => IsHost;
    public static bool CanDistributeLoot() => IsHost;

    // Bu client, PhotonView'in sahibi mi?
    public static bool OwnsView(PhotonView view) => view != null && view.IsMine;
    public static bool ControlsPlayer(PhotonView playerView) => OwnsView(playerView);

    // Host-only işlemler için guard: host değilse uyar ve false dön.
    public static bool RequireHost(string action = null)
    {
        if (IsHost) return true;
        if (!string.IsNullOrEmpty(action))
            Debug.LogWarning($"[AuthorityManager] '{action}' yalnizca host tarafindan yapilabilir.");
        return false;
    }
}
