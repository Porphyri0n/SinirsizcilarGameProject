using UnityEngine;
using Unity.Netcode;

// Düşman gemisi senkronu — Artık sadece temel NetworkBehaviour işlevlerini sağlar.
// Spawn/Despawn ve Can senkronu NetworkObject ve ShipHealth tarafından yönetilir.
// Pozisyon/Rotasyon senkronu prefab üzerindeki NetworkTransform tarafından yönetilir.
public class ShipNetSync : NetworkBehaviour
{
    // Bu sınıf şu an için boş bırakıldı, gelecekte gemiye özel ağ mantığı eklenirse kullanılabilir.
}
