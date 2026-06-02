using System;
using System.Collections;
using UnityEngine;
using Unity.Netcode;

// Yükseltme orkestratörü — maliyet kontrol, kaynak harca, süre say, sonunda IUpgradeable.Upgrade().
// Ocak, kule, el arabası yükseltmeleri buradan geçer.
// EventBus.FireUpgradeCompleted(targetName, newLevel) bitişte tetiklenir.
public class UpgradeManager : NetworkBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    // El arabası yükseltmesi tamamlanınca ekstra OnWheelbarrowUpgraded için targetName eşleşmesi.
    public const string TARGET_WHEELBARROW = "Wheelbarrow";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool IsUpgrading { get; private set; }
    public IUpgradeable CurrentTarget { get; private set; }
    public string CurrentTargetName { get; private set; }
    public float Progress { get; private set; }            // 0..1 — UI için

    // UI / efekt dinleyicileri için local event'ler
    public event Action<string, UpgradeData> OnUpgradeStarted;
    public event Action<string, UpgradeLevel> OnUpgradeFinished;

    // Yükseltmeyi başlat. targetName: "CraftingStation", "CannonTower", "Wheelbarrow" vb.
    // false döner: zaten yükseltme var, IUpgradeable null, next upgrade yok ya da maliyet yetersiz.
    public bool TryStartUpgrade(IUpgradeable target, string targetName)
    {
        if (IsUpgrading) return false;
        if (target == null) return false;
        if (!target.CanUpgrade()) return false;

        UpgradeData next = target.GetNextUpgrade();
        if (next == null) return false;

        // Client ise Server'a talep gönder
        if (IsClient && !IsServer)
        {
            // Bu projenin IUpgradeable implemente eden objeleri NetworkObject ID ile bulması gerekir.
            // Şimdilik basitleştirmek adına: Eğer target bir Component ise NetworkObject'ini bulabiliriz.
            var netObj = (target as Component)?.GetComponentInParent<NetworkObject>();
            if (netObj != null)
            {
                RequestUpgradeServerRpc(netObj.NetworkObjectId, targetName);
                return true; // Talep gönderildi
            }
            return false;
        }

        // Server ise veya single player ise işlemi başlat
        if (!HasResources(next)) return false;
        if (!SpendResources(next)) return false;

        StartUpgradeInternal(target, targetName, next);
        return true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestUpgradeServerRpc(ulong networkObjectId, string targetName)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
        {
            var target = netObj.GetComponentInChildren<IUpgradeable>();
            if (target != null)
            {
                TryStartUpgrade(target, targetName);
            }
        }
    }

    private void StartUpgradeInternal(IUpgradeable target, string targetName, UpgradeData next)
    {
        StartCoroutine(UpgradeRoutine(target, targetName, next));
        NotifyUpgradeStartedClientRpc(targetName, next.upgradeTime);
    }

    [ClientRpc]
    private void NotifyUpgradeStartedClientRpc(string targetName, float duration)
    {
        if (IsServer) return; // Zaten local coroutine çalışıyor
        
        // Client tarafında sadece görsel/UI takibi için coroutine başlat
        StartCoroutine(ClientProgressRoutine(targetName, duration));
    }

    private IEnumerator ClientProgressRoutine(string targetName, float duration)
    {
        IsUpgrading = true;
        CurrentTargetName = targetName;
        Progress = 0f;
        
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            Progress = Mathf.Clamp01(t / duration);
            yield return null;
        }

        IsUpgrading = false;
        Progress = 0f;
    }

    private bool HasResources(UpgradeData data)
    {
        EconomyManager econ = EconomyManager.Instance;
        if (econ == null) return false;
        if (data.cost == null) return true;

        foreach (RecipeIngredient ing in data.cost)
        {
            if (ing == null) continue;
            if (!econ.HasEnough(ing.resourceType, ing.amount)) return false;
        }
        return true;
    }

    private bool SpendResources(UpgradeData data)
    {
        EconomyManager econ = EconomyManager.Instance;
        if (econ == null) return false;
        if (data.cost == null) return true;

        foreach (RecipeIngredient ing in data.cost)
        {
            if (ing == null) continue;
            // HasResources önce geçtiği için pratikte false dönmez
            if (!econ.SpendResource(ing.resourceType, ing.amount)) return false;
        }
        return true;
    }

    private IEnumerator UpgradeRoutine(IUpgradeable target, string targetName, UpgradeData data)
    {
        IsUpgrading = true;
        CurrentTarget = target;
        CurrentTargetName = targetName;
        Progress = 0f;
        OnUpgradeStarted?.Invoke(targetName, data);

        float duration = Mathf.Max(0.1f, data.upgradeTime);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            Progress = Mathf.Clamp01(t / duration);
            yield return null;
        }

        target.Upgrade();       // IUpgradeable kendi seviyesini yükseltir
        EventBus.FireUpgradeCompleted(targetName, target.CurrentLevel);

        // El arabası için ayrıca özel event — Koray (kapasite/hız) ve Ziya (UI) dinler
        if (targetName == TARGET_WHEELBARROW)
            EventBus.FireWheelbarrowUpgraded(target.CurrentLevel);

        OnUpgradeFinished?.Invoke(targetName, target.CurrentLevel);

        IsUpgrading = false;
        CurrentTarget = null;
        CurrentTargetName = null;
        Progress = 0f;
    }
}
