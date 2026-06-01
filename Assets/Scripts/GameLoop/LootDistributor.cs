using System;
using UnityEngine;

// Wave sonu loot dagiticisi.
// OnWaveEnd dinler; wave numarasi + performans (kalenin yedigi hasar) ile
// dusurulecek loot sayisini belirler, kale icine rastgele konumlarda
// OnLootDropped event'i ile dagitir. Boss'un kendi bonus loot'u (BossShip.OnSink)
// bunun haricindedir.
public class LootDistributor : MonoBehaviour
{
    public static LootDistributor Instance { get; private set; }

    [Header("Drop Bolgesi")]
    [SerializeField] private Transform castleCenter;
    [SerializeField] private float dropRadius = 12f;
    [SerializeField] private float dropY = -2f;                  // castle floor level

    [Header("Miktar")]
    [SerializeField] private int baseLootPerWave = 3;
    [SerializeField] private float perWaveBonus = 0.5f;         // her wave ek miktar
    [SerializeField] private int bossWaveBonus = 2;

    [Header("Performans Carpani")]
    [Tooltip("Kale hic hasar yememisse loot 1.0x bu degerle carpilir; cok hasar yemisse 1.0x kalir")]
    [SerializeField] private float perfectRunMultiplier = 1.5f;
    [SerializeField] private float minPerformanceMultiplier = 0.8f;

    [Header("Tip Dagilimi")]
    [Range(0f, 1f)]
    [SerializeField] private float potionChance = 0.25f;

    [Header("Prefablar")]
    [SerializeField] private GameObject resourceLootPrefab;
    [SerializeField] private GameObject potionLootPrefab;

    private float castleHpAtWaveStart;
private bool tracking;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        EventBus.OnWaveStart += HandleWaveStart;
        EventBus.OnWaveEnd += HandleWaveEnd;
        EventBus.OnGameRestart += HandleGameRestart;
    }

    private void OnDisable()
    {
        EventBus.OnWaveStart -= HandleWaveStart;
        EventBus.OnWaveEnd -= HandleWaveEnd;
        EventBus.OnGameRestart -= HandleGameRestart;
    }

    private void HandleGameRestart()
    {
        tracking = false;
        castleHpAtWaveStart = 0f;
    }

    private void HandleWaveStart(int waveNumber)
    {
        castleHpAtWaveStart = CurrentCastleHp();
        tracking = true;
    }

    private void HandleWaveEnd(int waveNumber)
    {
        int count = ComputeLootCount(waveNumber);
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = RandomDropPosition();
            LootType type = PickLootType();
            
            SpawnLoot(pos, type);
            EventBus.FireLootDropped(pos, type);
        }
        tracking = false;
    }

    private void SpawnLoot(Vector3 pos, LootType type)
    {
        GameObject prefab = type == LootType.Potion ? potionLootPrefab : resourceLootPrefab;
        if (prefab == null) return;

        GameObject obj = Instantiate(prefab, pos + Vector3.up * 5f, Quaternion.identity);
        
        // Yere düşme hissi için ufak bir rastgele itiş
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 force = new Vector3(UnityEngine.Random.Range(-1f, 1f), 2f, UnityEngine.Random.Range(-1f, 1f));
            rb.AddForce(force, ForceMode.Impulse);
        }
    }

    private int ComputeLootCount(int waveNumber)
    {
        float baseCount = baseLootPerWave + perWaveBonus * Mathf.Max(0, waveNumber - 1);
        if (WaveScaler.IsBossWave(waveNumber)) baseCount += bossWaveBonus;

        float perf = PerformanceMultiplier();
        int count = Mathf.Max(1, Mathf.RoundToInt(baseCount * perf));
        return count;
    }

    // 1.0 = beklenen / 1.5 = sifir hasar / 0.8 = kale neredeyse yikilmis
    private float PerformanceMultiplier()
    {
        if (!tracking) return 1f;
        float now = CurrentCastleHp();
        float max = CastleHealth.Instance != null ? CastleHealth.Instance.MaxHealth : GameConstants.CASTLE_MAX_HP;
        if (max <= 0f) return 1f;

        float damageTaken = Mathf.Max(0f, castleHpAtWaveStart - now);
        float damageRatio = Mathf.Clamp01(damageTaken / max);

        // damageRatio 0 -> perfectRunMultiplier, damageRatio 1 -> minPerformanceMultiplier
        return Mathf.Lerp(perfectRunMultiplier, minPerformanceMultiplier, damageRatio);
    }

    private float CurrentCastleHp()
    {
        return CastleHealth.Instance != null ? CastleHealth.Instance.CurrentHealth : GameConstants.CASTLE_MAX_HP;
    }

    private Vector3 RandomDropPosition()
    {
        Vector3 center = castleCenter != null ? castleCenter.position : transform.position;
        Vector2 r = UnityEngine.Random.insideUnitCircle * dropRadius;
        return new Vector3(center.x + r.x, dropY, center.z + r.y);
    }

    private LootType PickLootType()
    {
        return UnityEngine.Random.value < potionChance ? LootType.Potion : LootType.Resource;
    }
}
