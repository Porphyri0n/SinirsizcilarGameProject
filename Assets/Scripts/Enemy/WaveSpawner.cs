using System;
using System.Collections;
using UnityEngine;

// Dalga başlayınca WaveData'daki gemileri sırayla (spawnInterval aralıklarla) kuzeyden spawn eder.
// ObjectPooler varsa onu kullanır; her gemi için EventBus.FireShipSpawned çağrılır.
public class WaveSpawner : MonoBehaviour
{
    [SerializeField] private WaveData[] waves;
    [SerializeField] private Transform[] spawnPoints;       // Kuzeyde (denizde) spawn noktaları
    [SerializeField] private Transform shoreTarget;         // Gemilerin yöneleceği sahil noktası
    [SerializeField] private ShipFormation formation;       // sahilde gemileri dağıtır (üst üste binmesin)

    private Coroutine spawnRoutine;
    private int nextSpawnPointIndex;
    private int formationIndex;                             // o dalgada kaçıncı gemi — formasyon slotu için

    private void Awake()
    {
        if (formation == null) formation = GetComponent<ShipFormation>();
    }

    private void OnEnable()
    {
        EventBus.OnWaveStart += HandleWaveStart;
        EventBus.OnWaveEnd += HandleWaveEnd;
    }

    private void OnDisable()
    {
        EventBus.OnWaveStart -= HandleWaveStart;
        EventBus.OnWaveEnd -= HandleWaveEnd;
    }

    private void HandleWaveStart(int waveNumber)
    {
        WaveData wave = GetWaveData(waveNumber);
        if (wave == null) return;

        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnWave(wave));
    }

    private void HandleWaveEnd(int waveNumber)
    {
        if (spawnRoutine == null) return;
        StopCoroutine(spawnRoutine);
        spawnRoutine = null;
    }

    private IEnumerator SpawnWave(WaveData wave)
    {
        float interval = Mathf.Max(0.05f, wave.spawnInterval);
        formationIndex = 0;     // her dalga formasyonu baştan dizilir

        if (wave.ships != null)
        {
            foreach (WaveShipEntry entry in wave.ships)
            {
                if (entry == null || entry.shipData == null) continue;
                for (int i = 0; i < entry.count; i++)
                {
                    SpawnShip(entry.shipData);
                    yield return new WaitForSeconds(interval);
                }
            }
        }
        spawnRoutine = null;
    }

    private void SpawnShip(ShipData shipData)
    {
        if (shipData.prefab == null) return;

        Transform point = NextSpawnPoint();
        Vector3 pos = point != null ? point.position : transform.position;
        // Apply 90 degree offset to fix sideways orientation
        Quaternion rot = point != null ? point.rotation * Quaternion.Euler(0, 90, 0) : Quaternion.Euler(0, 90, 0);

        GameObject ship = SpawnObject(shipData.prefab, pos, rot);

        ShipMovement movement = ship.GetComponent<ShipMovement>();
        if (movement != null)
        {
            if (shoreTarget != null) movement.SetShoreTarget(shoreTarget);
            Vector3 offset = formation != null ? formation.GetOffset(formationIndex) : Vector3.zero;
            movement.SetFormationOffset(offset);
        }
        formationIndex++;

        EventBus.FireShipSpawned(shipData.shipType);
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

    private Transform NextSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return null;
        Transform point = spawnPoints[nextSpawnPointIndex % spawnPoints.Length];
        nextSpawnPointIndex++;
        return point;
    }

    private WaveData GetWaveData(int waveNumber)
    {
        if (waves == null || waves.Length == 0) return null;
        foreach (WaveData w in waves)
            if (w != null && w.waveNumber == waveNumber) return w;
        return waves[waves.Length - 1];     // tanımlı setin ötesi: son config tekrar kullanılır
    }
}
