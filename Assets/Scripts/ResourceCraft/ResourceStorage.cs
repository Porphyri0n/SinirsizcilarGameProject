using System;
using System.Collections.Generic;
using UnityEngine;

// Kale deposu (craft ocağındaki stok). EconomyManager ile EventBus üzerinden senkron.
public class ResourceStorage : MonoBehaviour
{
    private readonly Dictionary<ResourceType, int> stock = new Dictionary<ResourceType, int>();

    public event Action OnChanged;

    public void Deposit(ResourceType type, int amount)
    {
        if (amount <= 0) return;
        stock.TryGetValue(type, out int current);
        stock[type] = current + amount;
        EventBus.FireResourceDeposited(type, amount);
        OnChanged?.Invoke();
    }

    public bool Withdraw(ResourceType type, int amount)
    {
        if (amount <= 0 || GetCount(type) < amount) return false;
        stock[type] -= amount;
        OnChanged?.Invoke();
        return true;
    }

    public int GetCount(ResourceType type)
    {
        stock.TryGetValue(type, out int current);
        return current;
    }

    public bool HasEnough(ResourceType type, int amount) => GetCount(type) >= amount;
}
