using System;
using UnityEngine;
using Unity.Netcode;

// Kervan yaklaşırken haydut pusu kurar. Şans tutarsa ticaret yolundaki ağaçlardan haydut spawn eder.
// Şans wave ilerledikçe artar: BANDIT_BASE_CHANCE + wave * BANDIT_CHANCE_INCREASE.
// Pusu kurulunca EventBus.FireBanditRaid(count, position) ile herkese haber verilir.
public class BanditSpawner : MonoBehaviour
{
    [SerializeField] private BanditData[] banditTypes;      // Raider, Brute SO'ları
    [SerializeField] private Transform[] ambushPoints;      // Ağaçlık alandaki pusu noktaları (doğu/batı yolu)
    [SerializeField] private int minBandits = 2;
    [SerializeField] private int maxBandits = 4;
    [SerializeField] private float spawnSpread = 1.5f;      // Aynı noktaya yığılmasınlar diye dağıtma yarıçapı
    [SerializeField] private float prepRaidInterval = 30f;  // Prep fazında kervandan bağımsız kaç saniyede bir haydut gelsin

    private int currentWave;
    private int banditsRemainingInPhase;

    private bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    private void OnEnable()
    {
        EventBus.OnCaravanApproaching += HandleCaravanApproaching;
        EventBus.OnWaveStart += HandleWaveStart;
        EventBus.OnPhaseChanged += HandlePhaseChanged;
    }

    private void OnDisable()
    {
        EventBus.OnCaravanApproaching -= HandleCaravanApproaching;
        EventBus.OnWaveStart -= HandleWaveStart;
        EventBus.OnPhaseChanged -= HandlePhaseChanged;
    }

    private void HandleWaveStart(int waveNumber)
    {
        currentWave = waveNumber;       // şans hesabı için en güncel wave'i tut
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        if (!IsServer) return;

        if (phase == GamePhase.Prep)
        {
            // Hazırlık aşaması başında bütçeyi belirle: ilk sefere 5, sonra her dalga +4
            banditsRemainingInPhase = 5 + (currentWave * 4);
            Debug.Log($"[BanditSpawner] Prep phase started. Bandit budget for this phase: {banditsRemainingInPhase}");
        }
    }

    // Kervan (CaravanController.Launch) yaklaşırken tetiklenir.
    private void HandleCaravanApproaching(CaravanData caravan)
    {
        if (!IsServer) return;

        // Kullanıcı isteği: Sadece hazırlık aşamasında kervanlarla beraber spawn olsunlar.
        // Toplam bütçe bitene kadar kervanlara dağıtıyoruz.
        if (banditsRemainingInPhase <= 0) return;

        // Bu kervanla kaç haydut geleceğini rastgele seçiyoruz (0, 1 veya 2)
        // Böylece her kervan baskın yapmaz, "hangi karavanla geleceği spawnera kalmış" olur.
        int toSpawn = UnityEngine.Random.Range(0, 3); 
        toSpawn = Mathf.Min(toSpawn, banditsRemainingInPhase);

        if (toSpawn <= 0) return;

        Vector3 ambush = PickAmbushPoint();
        Debug.Log($"[BanditSpawner] Caravan approaching. Spawning {toSpawn} bandits from budget. Remaining: {banditsRemainingInPhase - toSpawn}");

        for (int i = 0; i < toSpawn; i++)
            SpawnBandit(ambush);

        banditsRemainingInPhase -= toSpawn;
        EventBus.FireBanditRaid(toSpawn, ambush);
    }

    private bool RollForRaid()
    {
        // İlk dalgada (wave 0) kervan baskınlarını devre dışı bırakıyoruz, 
        // çünkü toplam 5 haydut zaten hazırlık (prep) aşamasında spawn ediliyor.
        if (currentWave == 0) return false;

        float chance = Mathf.Clamp01(GameConstants.BANDIT_BASE_CHANCE + currentWave * GameConstants.BANDIT_CHANCE_INCREASE);
        return UnityEngine.Random.value < chance;
    }

    private void SpawnBandit(Vector3 center)
    {
        BanditData data = PickBanditData();
        if (data == null) { Debug.LogError("[BanditSpawner] PickBanditData returned null!"); return; }
        if (data.prefab == null) { Debug.LogError($"[BanditSpawner] Prefab for {data.name} is null!"); return; }

        Vector2 offset = UnityEngine.Random.insideUnitCircle * spawnSpread;
        Vector3 pos = center + new Vector3(offset.x, 0f, offset.y);

        Debug.Log($"[BanditSpawner] Instantiating bandit prefab: {data.prefab.name} at {pos}");
        GameObject bandit = SpawnObject(data.prefab, pos, Quaternion.identity);

        if (bandit != null)
        {
            // Network üzerinden tüm client'larda spawn et
            NetworkObject netObj = bandit.GetComponent<NetworkObject>();
            if (netObj != null && !netObj.IsSpawned) 
                netObj.Spawn();

            BanditHealth health = bandit.GetComponent<BanditHealth>();
            if (health != null) health.Configure(data);
            Debug.Log("[BanditSpawner] Bandit spawned and configured successfully.");
        }
        else
        {
            Debug.LogError("[BanditSpawner] Failed to instantiate bandit!");
        }
    }

    private BanditData PickBanditData()
    {
        if (banditTypes == null || banditTypes.Length == 0) return null;
        return banditTypes[UnityEngine.Random.Range(0, banditTypes.Length)];
    }

    // Manuel veya dış sistemlerden tetiklemek için.
    public void TriggerRaid(int count)
    {
        if (!IsServer) return;

        Vector3 ambush = PickAmbushPoint();
        for (int i = 0; i < count; i++)
            SpawnBandit(ambush);

        EventBus.FireBanditRaid(count, ambush);
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
