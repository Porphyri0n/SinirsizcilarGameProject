using System;
using UnityEngine;

// Wave ilerleyisini yoneten singleton.
// OnPhaseChanged(Wave) ile yeni wave baslatir; WaveScaler'dan prosedurel plan alir.
// OnShipDestroyed dinleyerek kalan gemi sayacini guncel tutar; sayac 0'a inince OnWaveEnd.
// Boss wave'de ayrica OnBossWaveStart firelar. Gemilerin spawn'i WaveSpawner'a (Erdo) ait.
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [SerializeField] private int startingWave = 0;

    private int currentWave;
    private int remainingShips;
    private WavePlan currentPlan;
    private bool waveActive;
    private bool gameOver;   // Kale yıkıldıktan sonra yeni wave başlatmayı durdurur

    public int CurrentWave => currentWave;
    public int RemainingShips => remainingShips;
    public WavePlan CurrentPlan => currentPlan;
    public bool WaveActive => waveActive;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        currentWave = startingWave;
    }

    private void OnEnable()
    {
        EventBus.OnPhaseChanged += HandlePhaseChanged;
        EventBus.OnShipDestroyed += HandleShipDestroyed;
        EventBus.OnGameLost += HandleGameLost;
        EventBus.OnGameRestart += HandleGameRestart;
    }

    private void OnDisable()
    {
        EventBus.OnPhaseChanged -= HandlePhaseChanged;
        EventBus.OnShipDestroyed -= HandleShipDestroyed;
        EventBus.OnGameLost -= HandleGameLost;
        EventBus.OnGameRestart -= HandleGameRestart;
    }

    private void HandleGameRestart()
    {
        currentWave = startingWave;
        remainingShips = 0;
        currentPlan = default;
        waveActive = false;
        gameOver = false;
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        if (phase == GamePhase.Wave && !waveActive)
            StartNextWave();
    }

    // Bir sonraki wave'i baslatir: planlama + event yayinlama.
    public void StartNextWave()
    {
        if (gameOver || waveActive) return;   // oyun bittiyse / wave zaten aktifken yeni wave başlatma

        currentWave++;
        currentPlan = WaveScaler.Plan(currentWave);
        remainingShips = currentPlan.TotalShips;

        if (remainingShips <= 0)   // boş wave: sayaç hiç 0'a inemez, soft-lock olmasın diye hemen bitir
        {
            EventBus.FireWaveEnd(currentWave);
            return;
        }

        waveActive = true;
        EventBus.FireWaveStart(currentWave);
        if (currentPlan.isBossWave)
            EventBus.FireBossWaveStart(currentWave);
    }

    // Wave'i zorla bitirir (Skip için).
    public void ForceEndWave()
    {
        if (!waveActive) return;
        
        waveActive = false;
        remainingShips = 0;
        EventBus.FireWaveEnd(currentWave);
    }

    private void HandleShipDestroyed(ShipType type, Vector3 pos)
{
        if (!waveActive) return;

        remainingShips = Mathf.Max(0, remainingShips - 1);
        if (remainingShips == 0)
        {
            waveActive = false;
            EventBus.FireWaveEnd(currentWave);
        }
    }

    private void HandleGameLost(int survivedWaves)
    {
        gameOver = true;
    }
}
