using System;
using System.Collections.Generic;
using UnityEngine;

// Oyuncunun üzerinde taşıdığı kaynaklar.
public class ResourceInventory : MonoBehaviour
{
    private readonly Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();

    public event Action OnChanged;

    public void Add(ResourceType type, int amount)
    {
        if (amount <= 0) return;
        resources.TryGetValue(type, out int current);
        resources[type] = current + amount;
        OnChanged?.Invoke();
    }

    public bool Remove(ResourceType type, int amount)
    {
        if (amount <= 0 || GetCount(type) < amount) return false;
        resources[type] -= amount;
        OnChanged?.Invoke();
        return true;
    }

    public int GetCount(ResourceType type)
    {
        resources.TryGetValue(type, out int current);
        return current;
    }

    public bool IsEmpty()
    {
        foreach (KeyValuePair<ResourceType, int> kv in resources)
            if (kv.Value > 0) return false;
        return true;
    }

    public void Clear()
    {
        resources.Clear();
        OnChanged?.Invoke();
    }
}
