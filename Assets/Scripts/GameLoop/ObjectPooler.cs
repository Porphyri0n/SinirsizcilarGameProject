using System;
using System.Collections.Generic;
using UnityEngine;

// Basit obje havuzu — sık spawn/despawn olan nesneler için (gemiler, projeler, loot).
// Anahtar: prefab adı. Spawn havuzdan alır ya da yeni üretir; Return geri koyar (deaktif).
public class ObjectPooler : MonoBehaviour
{
    public static ObjectPooler Instance { get; private set; }

    private readonly Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();
    private readonly Dictionary<GameObject, string> instanceKeys = new Dictionary<GameObject, string>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Havuzu önceden doldur — oyun başında çağır
    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null) return;

        Queue<GameObject> pool = GetPool(prefab.name);
        for (int i = 0; i < count; i++)
        {
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            instanceKeys[obj] = prefab.name;
            pool.Enqueue(obj);
        }
    }

    public GameObject Spawn(GameObject prefab) => Spawn(prefab, Vector3.zero, Quaternion.identity);

    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        Queue<GameObject> pool = GetPool(prefab.name);
        GameObject obj = pool.Count > 0 ? pool.Dequeue() : Instantiate(prefab);
        instanceKeys[obj] = prefab.name;

        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);
        return obj;
    }

    public void Return(GameObject obj)
    {
        if (obj == null) return;

        obj.SetActive(false);
        obj.transform.SetParent(transform);

        if (instanceKeys.TryGetValue(obj, out string key))
            GetPool(key).Enqueue(obj);
        else
            Destroy(obj);   // Havuzdan gelmemiş bir nesne — sadece yok et
    }

    private Queue<GameObject> GetPool(string key)
    {
        if (!pools.TryGetValue(key, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            pools[key] = pool;
        }
        return pool;
    }
}
