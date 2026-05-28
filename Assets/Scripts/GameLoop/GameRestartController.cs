using System;
using UnityEngine;

// Oyun bittikten sonra yeni bir oyun baslatmak icin tek giris noktasi.
// RestartGame cagrilinca OnGameRestart yayinlar; tum sistemler bunu dinleyip
// kendi state'lerini sifirlar (WaveManager, EconomyManager, CastleHealth vs).
// UI veya host input'u (R tusu, menu butonu) burayi cagirir.
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
        gameOver = false;
        EventBus.FireGameRestart();
    }

    private void HandleGameLost(int survivedWaves)
    {
        gameOver = true;
    }
}
