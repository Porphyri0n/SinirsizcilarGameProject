using UnityEngine;
using Unity.Netcode;

// Kervan doğurucu — GamePhaseController'dan gelen sinyale göre Prep fazında kervan spawn eder.
// Sadece Server/Host tarafında çalışır ve NetworkObject.Spawn() ile tüm client'larda oluşturur.
public class CaravanSpawner : NetworkBehaviour
{
    [Header("Ayarlar")]
    [SerializeField] private GameObject caravanPrefab;
    [SerializeField] private CaravanData caravanData;
    [SerializeField] private float spawnInterval = 20f;    // Prep fazında kaç saniyede bir kervan doğsun
    
    [Header("Konumlar")]
    [SerializeField] private Transform[] spawnPoints;      // Harita Güney (patika başı)
    [SerializeField] private Transform castleTarget;       // Kale içi teslimat noktası
    [SerializeField] private Transform exitTarget;         // Harita çıkış noktası

    private Transform spawnPoint; // Current picked spawn point for the instance
    private Coroutine periodicSpawnRoutine;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            EventBus.OnPhaseChanged += HandlePhaseChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            EventBus.OnPhaseChanged -= HandlePhaseChanged;
            if (periodicSpawnRoutine != null) StopCoroutine(periodicSpawnRoutine);
        }
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        // Gerekli veriler yoksa yüklemeyi dene
        if (caravanData == null)
        {
            caravanData = Resources.Load<CaravanData>("CD_BasicCaravan");
        }

        if (phase == GamePhase.Prep)
        {
            if (periodicSpawnRoutine != null) StopCoroutine(periodicSpawnRoutine);
            periodicSpawnRoutine = StartCoroutine(PeriodicSpawnRoutine());
        }
        else
        {
            if (periodicSpawnRoutine != null)
            {
                StopCoroutine(periodicSpawnRoutine);
                periodicSpawnRoutine = null;
            }
        }
    }

    private System.Collections.IEnumerator PeriodicSpawnRoutine()
    {
        // İlk kervan faz başladıktan kısa süre sonra gelsin
        yield return new WaitForSeconds(3f);

        while (true)
        {
            StartCoroutine(SpawnSequence());
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    // Manuel veya test amaçlı tetiklemek için
    public void TriggerSpawn()
    {
        StartCoroutine(SpawnSequence());
    }

    private System.Collections.IEnumerator SpawnSequence()
    {
        if (caravanPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[CaravanSpawner] Prefab veya SpawnPoints atanmamış!");
            yield break;
        }

        Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        if (sp == null) yield break;

        GameObject obj = Instantiate(caravanPrefab, sp.position, sp.rotation);

        // Network üzerinden tüm client'larda spawn et
        NetworkObject netObj = obj.GetComponent<NetworkObject>();
        if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) 
            netObj.Spawn();

        // Give it a frame for components to settle
        yield return null;

        if (obj == null) yield break;

        CaravanController controller = obj.GetComponent<CaravanController>();
        CaravanMovement movement = obj.GetComponent<CaravanMovement>();

        // Hedefleri ayarla (Host tarafında hareket hesaplandığı için yeterli)
        if (movement != null)
        {
            if (castleTarget != null) movement.SetCastleTarget(castleTarget);
            if (exitTarget != null) movement.SetExitTarget(exitTarget);
        }

        // Kervanı başlat (Kargo miktarını mevcut wave'e göre ölçekler)
        if (controller != null)
        {
            int currentWave = GamePhaseController.Instance != null ? GamePhaseController.Instance.UpcomingWave : 1;
            controller.Launch(caravanData, currentWave);
        }
        
        Debug.Log($"[CaravanSpawner] Kervan doğuruldu ve başlatıldı.");
    }

    private void SpawnCaravan()
    {
        StartCoroutine(SpawnSequence());
    }
}
