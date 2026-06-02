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
        // Eğer bu çanı biz tetiklediysek (veya server'sak) diğerlerine bildir
        // Not: BellSystem.RingBell her çağrıldığında bu event tetiklenir.
        // Sonsuz döngü olmaması için RPC'den gelen çağrılarda tekrar RPC göndermemeliyiz.
        if (IsOwner || IsServer)
        {
            // Bu basitleştirilmiş bir yaklaşım. Proje mimarisine göre 
            // sadece etkileşimi yapan client'ın RPC göndermesi beklenir.
            if (NetworkManager.Singleton.IsClient && !IsProxy())
            {
                RPC_NotifyBellRungRpc((int)signal);
            }
        }
    }

    [Rpc(SendTo.NotOwner)]
    private void RPC_NotifyBellRungRpc(int signalInt)
    {
        if (bell != null)
        {
            // Remote client'larda sadece görsel/işitsel çalma işlemini yap
            // RingBell içindeki EventBus.FireBellRung yerel UI'ları tetikleyecektir.
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
