using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// Loot bildirimi — OnLootDropped gelince düşen loot'un üzerinde parlama (glow) efekti yakar
// ve yakındaki yerel oyuncuya "[E] Al" prompt'u gösterir (diegetic).
// Glow loot'a child olarak bağlanır; loot toplanınca (yok olunca) glow da otomatik gider.
public class LootPickupUI : MonoBehaviour
{
    [Header("Parlama Efekti")]
    [SerializeField] private GameObject resourceGlowPrefab;     // Kaynak loot parlaması
    [SerializeField] private GameObject potionGlowPrefab;       // İksir loot parlaması
    [SerializeField] private float searchRadius = 2f;           // Event pozisyonuna en yakın loot'u bulma yarıçapı

    [Header("Prompt")]
    [SerializeField] private TMP_Text pickupLabel;              // World-space "[E] Al" yazısı
    [SerializeField] private string pickupText = "[E] Al";
    [SerializeField] private float verticalOffset = 1.2f;
    [SerializeField] private Camera viewCamera;                 // boş ise Camera.main

    private readonly List<Transform> trackedLoot = new List<Transform>();
    private GameObject localPlayer;

    private void Awake()
    {
        if (pickupLabel != null) pickupLabel.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        EventBus.OnLootDropped += HandleLootDropped;
    }

    private void OnDisable()
    {
        EventBus.OnLootDropped -= HandleLootDropped;
    }

    private void HandleLootDropped(Vector3 position, LootType type)
    {
        Transform loot = FindLootNear(position);
        if (loot == null || trackedLoot.Contains(loot)) return;

        GameObject glowPrefab = GlowFor(type);
        if (glowPrefab != null)
            Instantiate(glowPrefab, loot.position, Quaternion.identity, loot);   // loot'la birlikte yok olur

        trackedLoot.Add(loot);
    }

    private GameObject GlowFor(LootType type)
    {
        return type == LootType.Potion ? potionGlowPrefab : resourceGlowPrefab;
    }

    private Transform FindLootNear(Vector3 position)
    {
        GameObject[] loots = GameObject.FindGameObjectsWithTag(GameConstants.TAG_LOOT);
        Transform nearest = null;
        float best = searchRadius * searchRadius;
        foreach (GameObject loot in loots)
        {
            float sqr = (loot.transform.position - position).sqrMagnitude;
            if (sqr <= best)
            {
                best = sqr;
                nearest = loot.transform;
            }
        }
        return nearest;
    }

    private void LateUpdate()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        if (localPlayer == null) localPlayer = GameObject.FindGameObjectWithTag(GameConstants.TAG_PLAYER);

        Transform nearest = PruneAndFindNearestInRange();
        ShowPrompt(nearest);
    }

    // Toplanan/yok olan loot'ları listeden düşür, yerel oyuncuya menzildeki en yakın loot'u döndür.
    private Transform PruneAndFindNearestInRange()
    {
        Transform nearest = null;
        float best = GameConstants.INTERACT_RANGE * GameConstants.INTERACT_RANGE;

        for (int i = trackedLoot.Count - 1; i >= 0; i--)
        {
            Transform loot = trackedLoot[i];
            if (loot == null || !loot.gameObject.activeInHierarchy)
            {
                trackedLoot.RemoveAt(i);
                continue;
            }
            if (localPlayer == null) continue;

            float sqr = (loot.position - localPlayer.transform.position).sqrMagnitude;
            if (sqr <= best)
            {
                best = sqr;
                nearest = loot;
            }
        }
        return nearest;
    }

    private void ShowPrompt(Transform loot)
    {
        if (pickupLabel == null) return;

        if (loot == null || viewCamera == null)
        {
            pickupLabel.gameObject.SetActive(false);
            return;
        }

        pickupLabel.gameObject.SetActive(true);
        pickupLabel.text = pickupText;
        pickupLabel.transform.position = loot.position + Vector3.up * verticalOffset;
        // Billboard — yüzü kameraya dönük
        pickupLabel.transform.rotation =
            Quaternion.LookRotation(pickupLabel.transform.position - viewCamera.transform.position);
    }
}
