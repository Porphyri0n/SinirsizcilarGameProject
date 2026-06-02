using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Çan kulesi senkronizasyonu. 
/// Bir oyuncu çan çaldığında bunu tüm client'lara RPC ile iletir.
/// </summary>
public class BellNetSync : NetworkBehaviour
{
    [SerializeField] private BellSystem bell;

    private void Awake()
    {
        if (bell == null) bell = GetComponent<BellSystem>();
    }

    private void OnEnable()
    {
        // Sadece yerel etkileşim sonucu tetiklenen event'i dinle
        EventBus.OnBellRung += HandleBellRung;
    }

    private void OnDisable()
    {
        EventBus.OnBellRung -= HandleBellRung;
    }

    private void HandleBellRung(BellSignal signal)
    {
        // Sonsuz döngü ve yetki hatasını önlemek için:
        // Sadece yerel etkileşim ise Server'dan dağıtım talep et.
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsClient)
        {
            // Eğer biz çaldıysak (isProxy değilsek)
            if (!IsProxy())
                RequestRingBellServerRpc((int)signal);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestRingBellServerRpc(int signalInt)
    {
        // Server tüm client'lara (NotOwner = tüm client'lar, çünkü server owner'dır) dağıtır.
        RPC_NotifyBellRungRpc(signalInt);
    }

    [Rpc(SendTo.NotOwner, Delivery = RpcDelivery.Unreliable)]
    private void RPC_NotifyBellRungRpc(int signalInt)
    {
        if (bell != null)
        {
            // Remote client'larda sadece görsel/işitsel çalma işlemini yap
            bell.RingBell((BellSignal)signalInt);
        }
    }

    private bool IsProxy()
    {
        // Eğer bu obje bizim tarafımızdan spawn edilmediyse veya owner değilsek proxy'dir.
        // Ancak çan gibi statik objelerde NetworkObject.IsOwner kullanımı kurulumuna bağlıdır.
        return !IsOwner && !IsServer;
    }
}
