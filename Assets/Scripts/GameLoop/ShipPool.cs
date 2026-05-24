using System;
using System.Collections.Generic;
using UnityEngine;

// ObjectPooler ile gemi spawn entegrasyonu (Singleton).
// Havuzları ShipType bazlı önceden doldurur (prewarm) ve WaveSpawner'ın
// ShipType ile gemi spawn etmesi için hazır bir arayüz sunar.
// ObjectPooler jenerik kalır (prefab adı bazlı); tür->prefab eşlemesi burada tutulur.
public class ShipPool : MonoBehaviour
{
    public static ShipPool Instance { get; private set; }

    [Header("Gemi Konfigürasyonları")]
    [SerializeField] private ShipData[] shipConfigs;        // Light, Medium, Heavy, Boss
    [SerializeField] private int prewarmPerType = 4;        // Tür başına önceden üretilecek adet

    private readonly Dictionary<ShipType, ShipData> byType = new Dictionary<ShipType, ShipData>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildLookup();
    }

    private void Start()
    {
        PrewarmAll();
    }

    private void BuildLookup()
    {
        byType.Clear();
        if (shipConfigs == null) return;
        foreach (ShipData data in shipConfigs)
        {
            if (data == null) continue;
            byType[data.shipType] = data;
        }
    }

    // Her ShipType için havuzu önceden doldur — wave başında spawn takılmasın.
    public void PrewarmAll()
    {
        if (ObjectPooler.Instance == null) return;
        foreach (KeyValuePair<ShipType, ShipData> kv in byType)
        {
            GameObject prefab = kv.Value != null ? kv.Value.prefab : null;
            if (prefab != null)
                ObjectPooler.Instance.Prewarm(prefab, prewarmPerType);
        }
    }

    public GameObject GetPrefab(ShipType type)
        => byType.TryGetValue(type, out ShipData data) && data != null ? data.prefab : null;

    // WaveSpawner'ın kullanacağı arayüz: tür ile gemi spawn et (havuzdan, yoksa instantiate).
    public GameObject SpawnShip(ShipType type, Vector3 position, Quaternion rotation)
    {
        GameObject prefab = GetPrefab(type);
        if (prefab == null) return null;

        if (ObjectPooler.Instance != null)
            return ObjectPooler.Instance.Spawn(prefab, position, rotation);

        return Instantiate(prefab, position, rotation);
    }

    // Gemi battığında havuza geri ver — prewarm edilen havuz tekrar kullanılsın.
    public void ReturnShip(GameObject ship)
    {
        if (ship == null) return;
        if (ObjectPooler.Instance != null) ObjectPooler.Instance.Return(ship);
        else Destroy(ship);
    }
}
