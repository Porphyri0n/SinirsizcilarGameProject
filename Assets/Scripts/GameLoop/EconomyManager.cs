using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

// Küresel kaynak stoğu — kervanlardan gelen ve craft ocağına yatırılan kaynakları tutar. Singleton.
// Kale içinde kaynak node yok; her şey ticari kervanlardan gelir.
public class EconomyManager : NetworkBehaviour
{
    public static EconomyManager Instance { get; private set; }

    // ResourceType int casting ile senkronize edilecek. 
    // Not: Dictionary senkronizasyonu için NetworkVariable<NetworkDictionary> gibi bir şey gerekirdi.
    // Şimdilik basitleştirmek adına: Yerel state'i tutuyoruz ama işlemler RPC ile yapılıyor.
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
        EventBus.OnGameRestart += HandleGameRestart;
    }

    private void OnDisable()
    {
        EventBus.OnResourceReceived -= HandleResourceReceived;
        EventBus.OnResourceDeposited -= HandleResourceDeposited;
        EventBus.OnGameRestart -= HandleGameRestart;
    }

    private void HandleGameRestart() => globalStock.Clear();

    public int GetStock(ResourceType type) => globalStock.TryGetValue(type, out int amount) ? amount : 0;

    // Sadece Server ekleyebilir/çıkarabilir (Logic authoritative)
    public void AddResource(ResourceType type, int amount)
    {
        if (!IsServer) return;
        AddResourceInternal(type, amount);
        NotifyResourceChangedClientRpc((int)type, GetStock(type));
    }

    public bool HasEnough(ResourceType type, int amount) => GetStock(type) >= amount;

    public bool SpendResource(ResourceType type, int amount)
    {
        if (!IsServer) return false;
        if (amount <= 0 || !HasEnough(type, amount)) return false;
        
        globalStock[type] = GetStock(type) - amount;
        NotifyResourceChangedClientRpc((int)type, globalStock[type]);
        
        // Fire locally on server to trigger UI updates
        EventBus.FireResourceDeposited(type, 0);
        return true;
    }

    private void AddResourceInternal(ResourceType type, int amount)
    {
        if (amount <= 0) return;
        globalStock[type] = GetStock(type) + amount;
    }

    [ClientRpc]
    private void NotifyResourceChangedClientRpc(int typeInt, int newAmount)
    {
        if (IsServer) return;
        globalStock[(ResourceType)typeInt] = newAmount;
        Debug.Log($"[Economy] Client updated { (ResourceType)typeInt } to {newAmount}");
        
        // Fire locally on client to trigger UI updates
        EventBus.FireResourceDeposited((ResourceType)typeInt, 0);
    }

    private void HandleResourceReceived(ResourceType type, int amount) 
    {
        if (IsServer) AddResource(type, amount);
        else AddResourceInternal(type, amount); // Clients also update their local view
    }
    
    private void HandleResourceDeposited(ResourceType type, int amount)
    {
        if (IsServer) AddResource(type, amount);
        else AddResourceInternal(type, amount);
    }
}
