using Unity.Netcode;
using UnityEngine;

// Oyuncu spawn ve revive yaşam döngüsü.
// Başlangıçta spawn noktasına yerleştirir ve canı doldurur.
// Ölünce hareket/savaş/etkileşim kontrolünü kapatır; ragdoll fiziğini RagdollController yönetir.
// EventBus.OnPlayerRevived kendi ID'miz için gelince: can full + kontrol geri verilir
// (ragdoll kapatma RagdollController'da). Revive yerinde olur (sela cesedi diriltir), ışınlama yok.
public class PlayerSpawnController : NetworkBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerController movement;
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerInteraction interaction;

    [Header("Spawn")]
    [SerializeField] private Transform spawnPoint;          // boş ise başlangıç konumunda kalır

    [SerializeField] private int playerID = -1;             // Network katmanı atar (PlayerHealth ile aynı)

    private void Awake()
    {
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (movement == null) movement = GetComponent<PlayerController>();
        if (combat == null) combat = GetComponent<PlayerCombat>();
        if (interaction == null) interaction = GetComponent<PlayerInteraction>();
    }

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.OnDeath += HandleDeath;
        EventBus.OnPlayerRevived += HandleRevived;
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnDeath -= HandleDeath;
        EventBus.OnPlayerRevived -= HandleRevived;
    }

    public override void OnNetworkSpawn()
    {
        // Network ID'sini tüm bileşenlere yay
        int id = (int)OwnerClientId;
        SetPlayerID(id);
        if (playerHealth != null) playerHealth.SetPlayerID(id);
        
        // Diğer bileşenleri de bul ve set et
        GetComponent<PlayerCombat>()?.SetPlayerID(id);
        GetComponent<RagdollController>()?.SetPlayerID(id);
        GetComponent<WeaponManager>()?.SetPlayerID(id);
        GetComponent<PotionSystem>()?.SetPlayerID(id);

        // Sahibi olduğumuz (kendi) karakterimizi spawn noktasına yerleştiriyoruz.
        // Client-authoritative (Owner write) sistemlerde her oyuncu kendini ışınlamalıdır.
        if (IsOwner)
        {
            if (spawnPoint == null)
            {
                GameObject sp = GameObject.Find("PlayerSpawnPoint");
                if (sp != null) spawnPoint = sp.transform;
            }

            if (spawnPoint != null)
            {
                Vector3 pos = spawnPoint.position;
                // OwnerClientId'ye göre oyuncuları hafif kaydırarak iç içe geçmelerini önle.
                pos += new Vector3((OwnerClientId % 3) * 1.5f, 0, (OwnerClientId / 3) * 1.5f);
                
                TeleportTo(pos, spawnPoint.rotation);
            }

            if (playerHealth != null) playerHealth.ResetHealth();
        }
        
        // Oyun başlama durumunu kontrol et.
        bool gameStarted = false;
        if (GameStateSync.Instance != null && GameStateSync.Instance.IsSpawned)
        {
            gameStarted = GameStateSync.Instance.GameStarted.Value;
        }
        else if (GameNetworkManager.Instance != null)
        {
            gameStarted = GameNetworkManager.Instance.GameStarted;
        }
        else
        {
            // Eğer hiçbir sistem bulunamadıysa (örn. test sahneleri), kontrolü açık bırak.
            gameStarted = true;
        }

        if (gameStarted)
        {
            SetControlEnabled(true);
        }
        else
        {
            SetControlEnabled(false);
        }
    }

    private void HandleDeath()
    {
        // Ceset yerde kalır (sela için); hareket/savaş kapanır, fizik RagdollController'da
        SetControlEnabled(false);
    }

    private void HandleRevived(int revivedID)
    {
        if (revivedID != playerID) return;

        // Işınlanma: Başlangıç spawn noktasına geri dön.
        if (IsOwner)
        {
            if (spawnPoint == null)
            {
                GameObject sp = GameObject.Find("PlayerSpawnPoint");
                if (sp != null) spawnPoint = sp.transform;
            }

            if (spawnPoint != null)
            {
                Vector3 pos = spawnPoint.position;
                // OwnerClientId'ye göre oyuncuları hafif kaydırarak iç içe geçmelerini önle.
                pos += new Vector3((OwnerClientId % 3) * 1.5f, 0, (OwnerClientId / 3) * 1.5f);
                
                TeleportTo(pos, spawnPoint.rotation);
            }
        }

        if (IsServer && playerHealth != null) playerHealth.ResetHealth();   // can full
        SetControlEnabled(true);                                // kontrol geri ver
    }

    // Hareket/savaş/etkileşim scriptlerini topluca aç/kapat
    private void SetControlEnabled(bool enabled)
    {
        if (movement != null) movement.enabled = enabled;
        if (combat != null) combat.enabled = enabled;
        if (interaction != null) interaction.enabled = enabled;
    }

    // CharacterController açıkken transform.position güvenilir değil; CC'yi kapatıp konumla, sonra geri aç
    private void TeleportTo(Vector3 position, Quaternion rotation)
    {
        bool wasEnabled = characterController != null && characterController.enabled;
        if (characterController != null) characterController.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        if (characterController != null) characterController.enabled = wasEnabled;
    }

    // Network katmanı atar — PlayerHealth.SetPlayerID ile aynı ID kullanılmalı.
    public void SetPlayerID(int id) => playerID = id;
}
