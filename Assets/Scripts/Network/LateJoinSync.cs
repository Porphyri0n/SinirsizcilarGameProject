using System;
using UnityEngine;
using Unity.Netcode;

// GameStateSync faz/dalga/kale HP'sini senkronlar. LateJoinSync onun yanina
// devam eden oyuna katilan oyuncunun kacirdigi kalici state'i ekler:
//   - Kale deposundaki kaynak stoklari (her ResourceType icin)
//   - Game over bayragi (survivedWaves ile)
// Host EventBus event'lerinden okuyup client katildiginda RPC ile gonderir;
// geç katılan client OnNetworkSpawn'da Server'dan talep eder, Server da RPC ile gonderir.
public class LateJoinSync : NetworkBehaviour
{
    private int survivedWaves = -1;
    private bool isGameLost = false;

    public void OnEnable()
    {
        EventBus.OnGameLost += HandleGameLost;
    }

    public void OnDisable()
    {
        EventBus.OnGameLost -= HandleGameLost;
    }

    public override void OnNetworkSpawn()
    {
        if (IsClient && !IsServer)
        {
            RequestStateServerRpc();
        }
    }

    private void HandleGameLost(int waves)
    {
        if (IsServer)
        {
            isGameLost = true;
            survivedWaves = waves;
        }
    }

    // ── Client -> Server: Bilgi Talebi ───────────────────────────────────

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestStateServerRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        // Sadece pozitif stokları topla
        var values = (ResourceType[])Enum.GetValues(typeof(ResourceType));
        int count = 0;
        for (int i = 0; i < values.Length; i++)
            if (EconomyManager.Instance != null && EconomyManager.Instance.GetStock(values[i]) > 0) count++;

        int[] encoded = new int[count * 2];
        int idx = 0;
        for (int i = 0; i < values.Length; i++)
        {
            int stock = EconomyManager.Instance != null ? EconomyManager.Instance.GetStock(values[i]) : 0;
            if (stock <= 0) continue;
            encoded[idx++] = (int)values[i];
            encoded[idx++] = stock;
        }

        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };

        SendStateClientRpc(encoded, isGameLost, survivedWaves, clientRpcParams);
    }

    // ── Server -> Client: Bilgi Gönderimi ────────────────────────────────

    [ClientRpc]
    private void SendStateClientRpc(int[] encodedResources, bool gameLost, int waves, ClientRpcParams clientRpcParams = default)
    {
        if (IsServer) return; // Zaten Host'un kendi yerel state'i güncel

        // Kaynakları uygula
        for (int i = 0; i + 1 < encodedResources.Length; i += 2)
        {
            ResourceType type = (ResourceType)encodedResources[i];
            int amount = encodedResources[i + 1];
            if (amount > 0)
                EventBus.FireResourceReceived(type, amount);
        }

        // Game Over durumunu uygula
        if (gameLost)
        {
            EventBus.FireGameLost(waves);
        }
    }
}
