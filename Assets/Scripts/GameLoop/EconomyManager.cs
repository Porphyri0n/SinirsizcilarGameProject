using System;
using System.Collections.Generic;
using UnityEngine;

// Küresel kaynak stoğu — kervanlardan gelen ve craft ocağına yatırılan kaynakları tutar. Singleton.
// Kale içinde kaynak node yok; her şey ticari kervanlardan gelir.
public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    private readonly Dictionary<ResourceType, int> globalStock = new Dictionary<ResourceType, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        EventBus.OnResourceReceived += HandleResourceReceived;
        EventBus.OnResourceDeposited += HandleResourceDeposited;
    }

    private void OnDisable()
    {
        EventBus.OnResourceReceived -= HandleResourceReceived;
        EventBus.OnResourceDeposited -= HandleResourceDeposited;
    }

    public int GetStock(ResourceType type) => globalStock.TryGetValue(type, out int amount) ? amount : 0;

    public void AddResource(ResourceType type, int amount)
    {
        if (amount <= 0) return;
        globalStock[type] = GetStock(type) + amount;
    }

    public bool HasEnough(ResourceType type, int amount) => GetStock(type) >= amount;

    public bool SpendResource(ResourceType type, int amount)
    {
        if (amount <= 0 || !HasEnough(type, amount)) return false;
        globalStock[type] = GetStock(type) - amount;
        return true;
    }

    private void HandleResourceReceived(ResourceType type, int amount) => AddResource(type, amount);
    private void HandleResourceDeposited(ResourceType type, int amount) => AddResource(type, amount);
}
