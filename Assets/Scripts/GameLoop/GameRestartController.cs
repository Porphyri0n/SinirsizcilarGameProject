using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

// Oyun bittikten sonra yeni bir oyun baslatmak icin tek giris noktasi.
// RestartGame cagrilinca network'u kapatir ve sahneyi yeniden yukler.
// Boylece her sey (Lobi, oyuncular, dunya) sifirdan baslar.
public class GameRestartController : MonoBehaviour
{
    public static GameRestartController Instance { get; private set; }

    [SerializeField] private bool allowKeyboardRestart = true;
    [SerializeField] private KeyCode restartKey = KeyCode.R;

    private bool gameOver;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        EventBus.OnGameLost += HandleGameLost;
    }

    private void OnDisable()
    {
        EventBus.OnGameLost -= HandleGameLost;
    }

    private void Update()
    {
        if (!gameOver || !allowKeyboardRestart) return;
        if (Input.GetKeyDown(restartKey))
            RestartGame();
    }

    // Disaridan da cagrilabilir (UI butonu, network RPC).
    public void RestartGame()
    {
        Debug.Log("[GameRestartController] Restarting game via full scene reload.");
        
        if (GameNetworkManager.Instance != null)
        {
            GameNetworkManager.Instance.ResetGameState();
        }

        EventBus.ClearAll();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // Sahneyi yeniden yuklemek tum singleton'lari ve state'leri (GameNetworkManager haric DDOL olanlari) temizler.
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void HandleGameLost(int survivedWaves)
    {
        gameOver = true;
    }
}
