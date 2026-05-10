using System;
using UnityEngine;

// Sistemler arası iletişim — doğrudan referans yerine event üzerinden.
// Yayınlayan FireXxx çağırır, dinleyenler OnXxx'e abone olur (OnEnable'da +=, OnDisable'da -=).
public static class EventBus
{
    // ── GAME PHASE ──────────────────────────────────────────────────────
    public static event Action<GamePhase> OnPhaseChanged;
    public static void FirePhaseChanged(GamePhase phase) => OnPhaseChanged?.Invoke(phase);

    // ── WAVE ────────────────────────────────────────────────────────────
    public static event Action<int> OnWaveStart;
    public static void FireWaveStart(int waveNumber) => OnWaveStart?.Invoke(waveNumber);

    public static event Action<int> OnWaveEnd;
    public static void FireWaveEnd(int waveNumber) => OnWaveEnd?.Invoke(waveNumber);

    public static event Action<int> OnBossWaveStart;
    public static void FireBossWaveStart(int waveNumber) => OnBossWaveStart?.Invoke(waveNumber);

    // ── ENEMY SHIPS ─────────────────────────────────────────────────────
    public static event Action<ShipType, Vector3> OnShipDestroyed;
    public static void FireShipDestroyed(ShipType type, Vector3 pos) => OnShipDestroyed?.Invoke(type, pos);

    public static event Action<ShipType> OnShipSpawned;
    public static void FireShipSpawned(ShipType type) => OnShipSpawned?.Invoke(type);

    // ── CARAVAN (Ticari Kervan) ─────────────────────────────────────────
    public static event Action<CaravanData> OnCaravanApproaching;
    public static void FireCaravanApproaching(CaravanData data) => OnCaravanApproaching?.Invoke(data);

    public static event Action<CaravanData> OnCaravanArrived;
    public static void FireCaravanArrived(CaravanData data) => OnCaravanArrived?.Invoke(data);

    public static event Action<Vector3> OnCaravanUnderAttack;
    public static void FireCaravanUnderAttack(Vector3 pos) => OnCaravanUnderAttack?.Invoke(pos);

    public static event Action OnCaravanDestroyed;
    public static void FireCaravanDestroyed() => OnCaravanDestroyed?.Invoke();

    // ── BANDITS (Haydutlar) ─────────────────────────────────────────────
    public static event Action<int, Vector3> OnBanditRaid;     // banditCount, position
    public static void FireBanditRaid(int count, Vector3 pos) => OnBanditRaid?.Invoke(count, pos);

    public static event Action<BanditType, Vector3> OnBanditKilled;
    public static void FireBanditKilled(BanditType type, Vector3 pos) => OnBanditKilled?.Invoke(type, pos);

    // ── CASTLE ──────────────────────────────────────────────────────────
    public static event Action<float, float> OnCastleDamaged;
    public static void FireCastleDamaged(float current, float max) => OnCastleDamaged?.Invoke(current, max);

    public static event Action OnCastleDestroyed;
    public static void FireCastleDestroyed() => OnCastleDestroyed?.Invoke();

    // ── RESOURCES ───────────────────────────────────────────────────────
    public static event Action<ResourceType, int> OnResourceReceived;      // kervandan teslim alındı
    public static void FireResourceReceived(ResourceType t, int a) => OnResourceReceived?.Invoke(t, a);

    public static event Action<ResourceType, int> OnResourceDeposited;     // craft ocağına teslim edildi
    public static void FireResourceDeposited(ResourceType t, int a) => OnResourceDeposited?.Invoke(t, a);

    // ── CRAFTING ────────────────────────────────────────────────────────
    public static event Action<RecipeData> OnCraftCompleted;
    public static void FireCraftCompleted(RecipeData recipe) => OnCraftCompleted?.Invoke(recipe);

    public static event Action<RecipeData, float> OnCraftStarted;
    public static void FireCraftStarted(RecipeData r, float d) => OnCraftStarted?.Invoke(r, d);

    // ── UPGRADE ─────────────────────────────────────────────────────────
    public static event Action<string, UpgradeLevel> OnUpgradeCompleted;
    public static void FireUpgradeCompleted(string target, UpgradeLevel level) => OnUpgradeCompleted?.Invoke(target, level);

    // ── TOWERS ──────────────────────────────────────────────────────────
    public static event Action<int, DefenseType> OnTowerEntered;
    public static void FireTowerEntered(int pid, DefenseType t) => OnTowerEntered?.Invoke(pid, t);

    public static event Action<int, DefenseType> OnTowerExited;
    public static void FireTowerExited(int pid, DefenseType t) => OnTowerExited?.Invoke(pid, t);

    public static event Action<DefenseType, Vector3> OnTowerFired;
    public static void FireTowerFired(DefenseType t, Vector3 target) => OnTowerFired?.Invoke(t, target);

    // ── PLAYER ──────────────────────────────────────────────────────────
    public static event Action<int, Vector3> OnPlayerDied;
    public static void FirePlayerDied(int pid, Vector3 pos) => OnPlayerDied?.Invoke(pid, pos);

    public static event Action<int> OnPlayerRevived;
    public static void FirePlayerRevived(int pid) => OnPlayerRevived?.Invoke(pid);

    public static event Action<int, WeaponType> OnWeaponEquipped;
    public static void FireWeaponEquipped(int pid, WeaponType w) => OnWeaponEquipped?.Invoke(pid, w);

    // ── COMBAT ──────────────────────────────────────────────────────────
    public static event Action<int, float, Vector3> OnPlayerAttacked;
    public static void FirePlayerAttacked(int pid, float dmg, Vector3 pos) => OnPlayerAttacked?.Invoke(pid, dmg, pos);

    public static event Action<int, float> OnPlayerBlocked;
    public static void FirePlayerBlocked(int pid, float amt) => OnPlayerBlocked?.Invoke(pid, amt);

    // ── LOOT ────────────────────────────────────────────────────────────
    public static event Action<Vector3, LootType> OnLootDropped;
    public static void FireLootDropped(Vector3 pos, LootType t) => OnLootDropped?.Invoke(pos, t);

    // ── POTION ──────────────────────────────────────────────────────────
    public static event Action<int, PotionType, float> OnPotionUsed;
    public static void FirePotionUsed(int pid, PotionType t, float d) => OnPotionUsed?.Invoke(pid, t, d);

    // ── COMMUNICATION ───────────────────────────────────────────────────
    public static event Action<BellSignal> OnBellRung;
    public static void FireBellRung(BellSignal signal) => OnBellRung?.Invoke(signal);

    public static event Action<int, int> OnSelaStarted;
    public static void FireSelaStarted(int reader, int dead) => OnSelaStarted?.Invoke(reader, dead);

    // ── WHEELBARROW ─────────────────────────────────────────────────────
    public static event Action<UpgradeLevel> OnWheelbarrowUpgraded;
    public static void FireWheelbarrowUpgraded(UpgradeLevel level) => OnWheelbarrowUpgraded?.Invoke(level);

    // ── GAME END ────────────────────────────────────────────────────────
    public static event Action<int> OnGameLost;
    public static void FireGameLost(int survivedWaves) => OnGameLost?.Invoke(survivedWaves);

    // ── CLEANUP ─────────────────────────────────────────────────────────
    public static void ClearAll()
    {
        OnPhaseChanged = null;
        OnWaveStart = null;
        OnWaveEnd = null;
        OnBossWaveStart = null;
        OnShipDestroyed = null;
        OnShipSpawned = null;
        OnCaravanApproaching = null;
        OnCaravanArrived = null;
        OnCaravanUnderAttack = null;
        OnCaravanDestroyed = null;
        OnBanditRaid = null;
        OnBanditKilled = null;
        OnCastleDamaged = null;
        OnCastleDestroyed = null;
        OnResourceReceived = null;
        OnResourceDeposited = null;
        OnCraftCompleted = null;
        OnCraftStarted = null;
        OnUpgradeCompleted = null;
        OnTowerEntered = null;
        OnTowerExited = null;
        OnTowerFired = null;
        OnPlayerDied = null;
        OnPlayerRevived = null;
        OnWeaponEquipped = null;
        OnPlayerAttacked = null;
        OnPlayerBlocked = null;
        OnLootDropped = null;
        OnPotionUsed = null;
        OnBellRung = null;
        OnSelaStarted = null;
        OnWheelbarrowUpgraded = null;
        OnGameLost = null;
    }
}
