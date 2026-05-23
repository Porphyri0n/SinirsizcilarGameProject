using System;
using UnityEngine;

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

    private int currentWave;

    private void OnEnable()
    {
        EventBus.OnCaravanApproaching += HandleCaravanApproaching;
        EventBus.OnWaveStart += HandleWaveStart;
    }

    private void OnDisable()
    {
        EventBus.OnCaravanApproaching -= HandleCaravanApproaching;
        EventBus.OnWaveStart -= HandleWaveStart;
    }

    private void HandleWaveStart(int waveNumber)
    {
        currentWave = waveNumber;       // şans hesabı için en güncel wave'i tut
    }

    // Kervan (CaravanController.Launch) yaklaşırken tetiklenir.
    private void HandleCaravanApproaching(CaravanData caravan)
    {
        if (!RollForRaid()) return;

        Vector3 ambush = PickAmbushPoint();
        int count = UnityEngine.Random.Range(minBandits, maxBandits + 1);

        for (int i = 0; i < count; i++)
            SpawnBandit(ambush);

        EventBus.FireBanditRaid(count, ambush);
    }

    private bool RollForRaid()
    {
        float chance = Mathf.Clamp01(GameConstants.BANDIT_BASE_CHANCE + currentWave * GameConstants.BANDIT_CHANCE_INCREASE);
        return UnityEngine.Random.value < chance;
    }

    private void SpawnBandit(Vector3 center)
    {
        BanditData data = PickBanditData();
        if (data == null || data.prefab == null) return;

        Vector2 offset = UnityEngine.Random.insideUnitCircle * spawnSpread;
        Vector3 pos = center + new Vector3(offset.x, 0f, offset.y);

        GameObject bandit = SpawnObject(data.prefab, pos, Quaternion.identity);

        BanditHealth health = bandit.GetComponent<BanditHealth>();
        if (health != null) health.Configure(data);     // Raider/Brute verisini ver, canı sıfırla
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
