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

        // SUNUCU TARAFINDA: Tüm karakterleri spawn noktasına yerleştiriyoruz.
        // Bu sayede NetworkTransform üzerinden tüm client'lara doğru pozisyon senkronize edilir.
        if (IsServer)
        {
            MoveToSpawnPoint();
        }

        // SAHİBİ OLDUĞUMUZ (KENDİ) KARAKTERİMİZ İÇİN:
        if (IsOwner)
        {
            // Eğer sunucu değilsek (client isek), yerel olarak da bir kez taşıyalım (tahminleme/akıcılık için)
            if (!IsServer)
            {
                MoveToSpawnPoint();
            }

            if (playerHealth != null) playerHealth.ResetHealth();
        }
        
        // Oyun başlama durumunu kontrol et ve takip et.
        // GameStateSync'in henüz spawn olmamış olma ihtimaline karşı Coroutine ile bekliyoruz.
        StartCoroutine(InitializeControlState());
    }

    private System.Collections.IEnumerator InitializeControlState()
    {
        // GameStateSync'in hazır olmasını bekle (özellikle client'larda sahne geçişinde gecikebilir)
        int retryCount = 0;
        while (GameStateSync.Instance == null && retryCount < 100)
        {
            retryCount++;
            yield return null;
        }

        if (GameStateSync.Instance != null)
        {
            GameStateSync.Instance.GameStarted.OnValueChanged += OnGameStartedChanged;
            RefreshControlState(GameStateSync.Instance.GameStarted.Value);
        }
        else if (GameNetworkManager.Instance != null)
        {
            RefreshControlState(GameNetworkManager.Instance.GameStarted);
        }
        else
        {
            // Hiçbir sistem bulunamazsa varsayılan olarak kontrolleri açalım
            RefreshControlState(true);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (GameStateSync.Instance != null)
        {
            GameStateSync.Instance.GameStarted.OnValueChanged -= OnGameStartedChanged;
        }
    }

    private void OnGameStartedChanged(bool oldVal, bool newVal)
    {
        RefreshControlState(newVal);
    }

    private void RefreshControlState(bool gameStarted)
    {
        bool shouldEnable = gameStarted;

        // Kendi karakterimizse ve oyun sahnesindeysek kontrolleri açık tutuyoruz.
        // Bu sayede GameStateSync gecikse bile yerçekimi ve temel hareket çalışır.
        if (IsOwner && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("GameScene"))
        {
            shouldEnable = true;
        }

        if (shouldEnable)
        {
            SetControlEnabled(true);
        }
        else
        {
            // Lobi vb. durumlarda kontroller kapalı başlar
            SetControlEnabled(false);
        }
    }

    private void MoveToSpawnPoint()
    {
        Transform targetPoint = spawnPoint;

        if (targetPoint == null)
        {
            // "PlayerSpawnPoints" (plural) objesini ve çocuklarını kontrol et
            GameObject spGroup = GameObject.Find("PlayerSpawnPoints");
            if (spGroup != null && spGroup.transform.childCount > 0)
            {
                // OwnerClientId'ye göre 6 noktadan birini seç (veya kaç nokta varsa)
                int index = (int)(OwnerClientId % (ulong)spGroup.transform.childCount);
                targetPoint = spGroup.transform.GetChild(index);
            }
            else
            {
                // Eskisi gibi tekli "PlayerSpawnPoint" ara
                GameObject sp = GameObject.Find("PlayerSpawnPoint");
                if (sp != null) targetPoint = sp.transform;
            }
        }

        if (targetPoint != null)
        {
            TeleportTo(targetPoint.position, targetPoint.rotation);
        }
        else
        {
            // Eğer hiç nokta bulunamazsa, iç içe geçmemek için ufak bir kaydırma yap (0,0,0 olmasın diye)
            Vector3 fallbackPos = transform.position;
            fallbackPos += new Vector3((OwnerClientId % 3) * 1.5f, 0, (OwnerClientId / 3) * 1.5f);
            TeleportTo(fallbackPos, transform.rotation);
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
            MoveToSpawnPoint();
        }

        // Sunucu zaten ResetHealth() yapmış olmalı (RPC üzerinden), ancak güvenlik için burada da durabilir.
        if (IsServer && playerHealth != null) playerHealth.ResetHealth();   
        SetControlEnabled(true);                                // kontrol geri ver
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestReviveServerRpc()
    {
        if (!IsServer) return;
        
        if (playerHealth != null && !playerHealth.IsAlive)
        {
            Debug.Log($"[Server] Reviving player {playerID}");
            playerHealth.ResetHealth();
            NotifyRevivedClientRpc(playerID);
        }
    }

    [ClientRpc]
    private void NotifyRevivedClientRpc(int pid)
    {
        Debug.Log($"[Client] Received revive notification for player {pid}");
        EventBus.FirePlayerRevived(pid);
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
