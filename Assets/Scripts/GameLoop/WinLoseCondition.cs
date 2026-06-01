using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

// Oyun bitis kosulu. Sonsuz wave oldugu icin kazanma YOK, sadece kaybetme var.
// OnCastleDestroyed dinler ve survivedWaves ile OnGameLost firelar.
// Birden fazla tetiklemeyi engellemek icin tek seferlik kilit kullanir.
// Kale yikildiginda belirli bir sure sonra oyunu tamamen sifirlar (Lobiye doner).
public class WinLoseCondition : MonoBehaviour
{
    public static WinLoseCondition Instance { get; private set; }

    [SerializeField] private float autoRestartDelay = 5.0f;

    private bool gameEnded;

    public bool GameEnded => gameEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        EventBus.OnCastleDestroyed += HandleCastleDestroyed;
        EventBus.OnCastleDamaged += HandleCastleDamaged;
        EventBus.OnGameRestart += HandleGameRestart;
    }

    private void OnDisable()
    {
        EventBus.OnCastleDestroyed -= HandleCastleDestroyed;
        EventBus.OnCastleDamaged -= HandleCastleDamaged;
        EventBus.OnGameRestart -= HandleGameRestart;
    }

    private void HandleCastleDamaged(float current, float max)
    {
        if (current <= 0) HandleCastleDestroyed();
    }

    private void HandleGameRestart()
    {
        gameEnded = false;
        CancelInvoke(nameof(ExecuteFullRestart));
    }

    private void HandleCastleDestroyed()
    {
        if (gameEnded) return;
        gameEnded = true;

        int survivedWaves = WaveManager.Instance != null ? WaveManager.Instance.CurrentWave : 0;
        EventBus.FireGameLost(survivedWaves);

        // Kale yikildiktan sonra otomatik olarak lobiye don/oyunu yeniden yukle
        Invoke(nameof(ExecuteFullRestart), autoRestartDelay);
    }

    private void ExecuteFullRestart()
    {
        Debug.Log("[WinLoseCondition] Auto-restarting game... Shutting down network and reloading scene.");
        
        // Persist state'i sıfırla (DontDestroyOnLoad olduğu için manuel sıfırlama şart)
        if (GameNetworkManager.Instance != null)
        {
            GameNetworkManager.Instance.ResetGameState();
        }

        // EventBus'ı temizle
        EventBus.ClearAll();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // Aktif sahneyi yeniden yukle (bu sayede LobiManager ve NetworkManager sifirdan baslar)
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
