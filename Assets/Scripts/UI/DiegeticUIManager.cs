using System;
using System.Collections.Generic;
using UnityEngine;

// Dünya içi (diegetic) UI elementlerinin uyguladığı arayüz.
public interface IDiegeticUI
{
    Transform Anchor { get; }       // Görünürlüğün ölçüldüğü nokta
    float VisibleRange { get; }     // Bu mesafe içindeyse görünür
    void SetVisibility(bool visible);
}

// Diegetic UI elementlerinin merkezi yöneticisi.
// Elementler OnEnable'da Register, OnDisable'da Unregister çağırır.
// Yerel oyuncuya olan mesafeye göre her elementin görünürlüğünü açıp kapatır.
public class DiegeticUIManager : MonoBehaviour
{
    public static DiegeticUIManager Instance { get; private set; }

    [SerializeField] private float checkInterval = 0.2f;    // Görünürlük taraması aralığı (sn)

    private readonly List<IDiegeticUI> elements = new List<IDiegeticUI>();
    private Transform viewer;           // Yerel oyuncu — mesafe referansı
    private float nextCheckTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Register(IDiegeticUI element)
    {
        if (element != null && !elements.Contains(element))
            elements.Add(element);
    }

    public void Unregister(IDiegeticUI element) => elements.Remove(element);

    // Spawn kurulumu yerel oyuncuyu buradan bağlar.
    public void SetViewer(Transform localPlayer) => viewer = localPlayer;

    private void Update()
    {
        if (Time.time < nextCheckTime) return;
        nextCheckTime = Time.time + checkInterval;

        if (viewer == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(GameConstants.TAG_PLAYER);
            if (p == null) return;
            viewer = p.transform;
        }

        Vector3 viewerPos = viewer.position;
        for (int i = 0; i < elements.Count; i++)
        {
            IDiegeticUI e = elements[i];
            if (e == null || e.Anchor == null) continue;
            float range = e.VisibleRange;
            bool visible = (e.Anchor.position - viewerPos).sqrMagnitude <= range * range;
            e.SetVisibility(visible);
        }
    }
}
