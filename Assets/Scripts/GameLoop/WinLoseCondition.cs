using System;
using UnityEngine;

// Oyun bitis kosulu. Sonsuz wave oldugu icin kazanma YOK, sadece kaybetme var.
// OnCastleDestroyed dinler ve survivedWaves ile OnGameLost firelar.
// Birden fazla tetiklemeyi engellemek icin tek seferlik kilit kullanir.
public class WinLoseCondition : MonoBehaviour
{
    public static WinLoseCondition Instance { get; private set; }

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
    }

    private void OnDisable()
    {
        EventBus.OnCastleDestroyed -= HandleCastleDestroyed;
    }

    private void HandleCastleDestroyed()
    {
        if (gameEnded) return;
        gameEnded = true;

        int survivedWaves = WaveManager.Instance != null ? WaveManager.Instance.CurrentWave : 0;
        EventBus.FireGameLost(survivedWaves);
    }
}
