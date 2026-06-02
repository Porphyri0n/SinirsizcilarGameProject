using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

// Küresel kaynak stoğu — kervanlardan gelen ve craft ocağına yatırılan kaynakları tutar. Singleton.
// Kale içinde kaynak node yok; her şey ticari kervanlardan gelir.
public class EconomyManager : NetworkBehaviour
{
    public static EconomyManager Instance { get; private set; }

    // NetworkVariables for each resource type to ensure server authority and synchronization.
    public readonly NetworkVariable<int> woodAmount = new NetworkVariable<int>(0);
    public readonly NetworkVariable<int> stoneAmount = new NetworkVariable<int>(0);
    public readonly NetworkVariable<int> ironAmount = new NetworkVariable<int>(0);
    public readonly NetworkVariable<int> steelAmount = new NetworkVariable<int>(0);
    public readonly NetworkVariable<int> goldAmount = new NetworkVariable<int>(0);
    public readonly NetworkVariable<int> crystalAmount = new NetworkVariable<int>(0);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        // Subscribe to value changes to update local UI/state on all clients.
        woodAmount.OnValueChanged += (oldValue, newValue) => OnResourceChanged(ResourceType.Wood, newValue);
        stoneAmount.OnValueChanged += (oldValue, newValue) => OnResourceChanged(ResourceType.Stone, newValue);
        ironAmount.OnValueChanged += (oldValue, newValue) => OnResourceChanged(ResourceType.Iron, newValue);
        steelAmount.OnValueChanged += (oldValue, newValue) => OnResourceChanged(ResourceType.Steel, newValue);
        goldAmount.OnValueChanged += (oldValue, newValue) => OnResourceChanged(ResourceType.Gold, newValue);
        crystalAmount.OnValueChanged += (oldValue, newValue) => OnResourceChanged(ResourceType.Crystal, newValue);
    }

    private void OnEnable()
    {
        EventBus.OnGameRestart += HandleGameRestart;
    }

    private void OnDisable()
    {
        EventBus.OnGameRestart -= HandleGameRestart;
    }

    private void HandleGameRestart()
    {
        if (!IsServer) return;
        woodAmount.Value = 0;
        stoneAmount.Value = 0;
        ironAmount.Value = 0;
        steelAmount.Value = 0;
        goldAmount.Value = 0;
        crystalAmount.Value = 0;
    }

    private void OnResourceChanged(ResourceType type, int newValue)
    {
        Debug.Log($"[Economy] {type} changed to {newValue}");
        // Trigger UI refresh via the local event bus
        EventBus.FireResourceDeposited(type, 0);
    }

    public int GetStock(ResourceType type)
    {
        var variable = GetVariable(type);
        return variable != null ? variable.Value : 0;
    }

    private NetworkVariable<int> GetVariable(ResourceType type)
    {
        return type switch
        {
            ResourceType.Wood => woodAmount,
            ResourceType.Stone => stoneAmount,
            ResourceType.Iron => ironAmount,
            ResourceType.Steel => steelAmount,
            ResourceType.Gold => goldAmount,
            ResourceType.Crystal => crystalAmount,
            _ => null
        };
    }

    // Sadece Server ekleyebilir/çıkarabilir (Logic authoritative)
    public void AddResource(ResourceType type, int amount)
    {
        if (amount <= 0) return;

        if (IsServer)
        {
            var variable = GetVariable(type);
            if (variable != null)
            {
                variable.Value += amount;
            }
        }
        else
        {
            AddResourceServerRpc(type, amount);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddResourceServerRpc(ResourceType type, int amount)
    {
        // Server tarafında kaynağı ekle
        AddResource(type, amount);
    }

    public bool HasEnough(ResourceType type, int amount) => GetStock(type) >= amount;

    public bool SpendResource(ResourceType type, int amount)
    {
        if (!IsServer) return false;
        if (amount <= 0 || !HasEnough(type, amount)) return false;
        
        var variable = GetVariable(type);
        if (variable != null)
        {
            variable.Value -= amount;
            return true;
        }
        return false;
    }
}
