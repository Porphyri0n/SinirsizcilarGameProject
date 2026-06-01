using UnityEngine;
using Unity.Netcode;

// Kervan doğurucu — GamePhaseController'dan gelen sinyale göre Prep fazında kervan spawn eder.
// Sadece Server/Host tarafında çalışır ve NetworkObject.Spawn() ile tüm client'larda oluşturur.
public class CaravanSpawner : NetworkBehaviour
{
    [Header("Ayarlar")]
    [SerializeField] private GameObject caravanPrefab;
    [SerializeField] private CaravanData caravanData;
    [SerializeField] private float initialSpawnDelay = 3f; // Prep başladıktan kaç sn sonra (tek) kervan gelsin
    
    [Header("Konumlar")]
    [SerializeField] private Transform[] spawnPoints;      // Harita Güney (patika başı)
    [SerializeField] private Transform castleTarget;       // Kale içi teslimat noktası
    [SerializeField] private Transform exitTarget;         // Harita çıkış noktası

    private Transform spawnPoint; // Current picked spawn point for the instance
    private Coroutine singleSpawnRoutine;

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
            if (singleSpawnRoutine != null) StopCoroutine(singleSpawnRoutine);
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
            // Her prep'te SADECE BİR kervan gelsin.
            if (singleSpawnRoutine != null) StopCoroutine(singleSpawnRoutine);
            singleSpawnRoutine = StartCoroutine(SpawnOnceRoutine());
        }
        else
        {
            if (singleSpawnRoutine != null)
            {
                StopCoroutine(singleSpawnRoutine);
                singleSpawnRoutine = null;
            }
        }
    }

    private System.Collections.IEnumerator SpawnOnceRoutine()
    {
        // Faz başladıktan kısa süre sonra TEK kervan gelsin (prep başına bir kez)
        yield return new WaitForSeconds(initialSpawnDelay);
        StartCoroutine(SpawnSequence());
        singleSpawnRoutine = null;
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

        // Yeni kervan doğmadan önce, önceki prep'ten kalan (lootlanmamış/yok edilmemiş) kervanı temizle.
        DespawnExistingCaravans();

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

    // Sahnedeki tüm mevcut kervanları kaldırır (yeni kervan gelince eskisi yok olsun diye).
    private void DespawnExistingCaravans()
    {
        GameObject[] caravans = GameObject.FindGameObjectsWithTag(GameConstants.TAG_CARAVAN);
        foreach (GameObject go in caravans)
        {
            if (go == null) continue;
            NetworkObject netObj = go.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned) netObj.Despawn();
            else Destroy(go);
        }
    }

    private void SpawnCaravan()
    {
        StartCoroutine(SpawnSequence());
    }
}
