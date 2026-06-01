using System;
using System.Collections;
using UnityEngine;
using Unity.Netcode;

// Her prep fazında BİR kez haydut baskını kurar — kervan spawn olduktan 1-1.5sn sonra.
// Haydut sayısı yaklaşan wave'e göre ölçeklenir: her BOSS_WAVE_INTERVAL (5) wave'de bir artar.
// 5'in katı wave'lerde (5, 10, 15...) +1 boss haydut eklenir; başlarda (wave < 5) boss gelmez.
// Pusu kurulunca EventBus.FireBanditRaid(count, position) ile herkese haber verilir.
public class BanditSpawner : MonoBehaviour
{
    [SerializeField] private BanditData[] banditTypes;      // Raider, Brute SO'ları
    [SerializeField] private Transform[] ambushPoints;      // Ağaçlık alandaki pusu noktaları (doğu/batı yolu)
    [SerializeField] private float spawnSpread = 1.5f;      // Aynı noktaya yığılmasınlar diye dağıtma yarıçapı

    [Header("Dalga Ölçekleme")]
    [SerializeField] private int baseBanditCount = 3;           // İlk dalgalardaki temel haydut sayısı
    [SerializeField] private int extraBanditsPer5Waves = 2;     // Her 5 wave'de bir eklenen haydut sayısı
    [Tooltip("5'in katı wave'lerde gelen boss haydut. Atanmazsa boss spawn olmaz.")]
    [SerializeField] private BanditData bossBanditData;         // Boss haydut SO'su (Inspector'dan atanır)

    [Header("Zamanlama")]
    [SerializeField] private float banditSpawnDelayMin = 0.5f;  // Kervan spawn'ından sonra min bekleme
    [SerializeField] private float banditSpawnDelayMax = 0.5f;  // Kervan spawn'ından sonra max bekleme

    private bool raidSpawnedThisPrep;       // Her prep'te tek baskın olsun diye kilit
    private Coroutine raidRoutine;

    private bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    private void OnEnable()
    {
        EventBus.OnPhaseChanged += HandlePhaseChanged;
        EventBus.OnCaravanApproaching += HandleCaravanApproaching;
    }

    private void OnDisable()
    {
        EventBus.OnPhaseChanged -= HandlePhaseChanged;
        EventBus.OnCaravanApproaching -= HandleCaravanApproaching;
    }

    // Prep'e girince baskın kilidini sıfırla; prep dışına çıkınca bekleyen baskını iptal et.
    private void HandlePhaseChanged(GamePhase phase)
    {
        if (phase == GamePhase.Prep)
        {
            raidSpawnedThisPrep = false;
        }
        else if (raidRoutine != null)
        {
            StopCoroutine(raidRoutine);
            raidRoutine = null;
        }
    }

    // Kervan spawn olunca (FireCaravanApproaching) tetiklenir: 1-1.5sn sonra bu prep'in
    // tek haydut baskınını başlatır. Sadece server, prep başına bir kez.
    private void HandleCaravanApproaching(CaravanData data)
    {
        if (!IsServer) return;
        if (raidSpawnedThisPrep) return;

        raidSpawnedThisPrep = true;
        raidRoutine = StartCoroutine(SpawnRaidAfterDelay());
    }

    private IEnumerator SpawnRaidAfterDelay()
    {
        float delay = UnityEngine.Random.Range(banditSpawnDelayMin, banditSpawnDelayMax);
        yield return new WaitForSeconds(delay);
        SpawnPrepRaid();
        raidRoutine = null;
    }

    // Yaklaşan wave'e göre haydutları tek seferde spawn eder.
    private void SpawnPrepRaid()
    {
        int wave = CurrentWaveForScaling();

        // Temel sayı + her 5 wave'de bir artış (wave 1-4: base, 5-9: base+step, 10-14: base+2*step...)
        int count = baseBanditCount + (wave / GameConstants.BOSS_WAVE_INTERVAL) * extraBanditsPer5Waves;
        count = Mathf.Max(0, count);

        Vector3 ambush = PickAmbushPoint();
        int spawned = 0;

        for (int i = 0; i < count; i++)
            if (SpawnBandit(PickBanditData(), ambush)) spawned++;

        // Boss haydut yalnızca 5'in katı wave'lerde (5, 10, 15...); başlarda gelmez.
        bool bossDue = bossBanditData != null && WaveScaler.IsBossWave(wave);
        if (bossDue && SpawnBandit(bossBanditData, ambush)) spawned++;

        if (spawned > 0)
        {
            Debug.Log($"[BanditSpawner] Prep raid — wave {wave}: {spawned} haydut (boss: {bossDue}).");
            EventBus.FireBanditRaid(spawned, ambush);
        }
    }

    // Prep sırasında "o anki" wave = yaklaşan wave. GamePhaseController otorite kaynağı.
    private int CurrentWaveForScaling()
    {
        return GamePhaseController.Instance != null ? GamePhaseController.Instance.UpcomingWave : 1;
    }

    // Manuel veya dış sistemlerden tetiklemek için.
    public void TriggerRaid(int count)
    {
        if (!IsServer) return;

        Vector3 ambush = PickAmbushPoint();
        int spawned = 0;
        for (int i = 0; i < count; i++)
            if (SpawnBandit(PickBanditData(), ambush)) spawned++;

        if (spawned > 0)
            EventBus.FireBanditRaid(spawned, ambush);
    }

    private bool SpawnBandit(BanditData data, Vector3 center)
    {
        if (data == null) { Debug.LogError("[BanditSpawner] PickBanditData/bossBanditData null!"); return false; }
        if (data.prefab == null) { Debug.LogError($"[BanditSpawner] Prefab for {data.name} is null!"); return false; }

        Vector2 offset = UnityEngine.Random.insideUnitCircle * spawnSpread;
        Vector3 pos = center + new Vector3(offset.x, 0f, offset.y);

        GameObject bandit = SpawnObject(data.prefab, pos, Quaternion.identity);
        if (bandit == null) { Debug.LogError("[BanditSpawner] Failed to instantiate bandit!"); return false; }

        // Network üzerinden tüm client'larda spawn et
        NetworkObject netObj = bandit.GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsSpawned)
            netObj.Spawn();

        BanditHealth health = bandit.GetComponent<BanditHealth>();
        if (health != null) health.Configure(data);
        return true;
    }

    private BanditData PickBanditData()
    {
        if (banditTypes == null || banditTypes.Length == 0) return null;
        return banditTypes[UnityEngine.Random.Range(0, banditTypes.Length)];
    }

    private Vector3 PickAmbushPoint()
    {
        if (ambushPoints == null || ambushPoints.Length == 0) return transform.position;
        Transform point = ambushPoints[UnityEngine.Random.Range(0, ambushPoints.Length)];
        return point != null ? point.position : transform.position;
    }

    private GameObject SpawnObject(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (ObjectPooler.Instance != null)
        {
            GameObject pooled = ObjectPooler.Instance.Spawn(prefab);
            if (pooled != null)
            {
                pooled.transform.SetPositionAndRotation(pos, rot);
                pooled.SetActive(true);
                return pooled;
            }
        }
        return Instantiate(prefab, pos, rot);
    }
}
