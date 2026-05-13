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
    }

    private void OnDisable()
    {
        EventBus.OnPhaseChanged -= HandlePhaseChanged;
        EventBus.OnShipDestroyed -= HandleShipDestroyed;
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        if (phase == GamePhase.Wave && !waveActive)
            StartNextWave();
    }

    // Bir sonraki wave'i baslatir: planlama + event yayinlama.
    public void StartNextWave()
    {
        currentWave++;
        currentPlan = WaveScaler.Plan(currentWave);
        remainingShips = currentPlan.TotalShips;
        waveActive = true;

        EventBus.FireWaveStart(currentWave);
        if (currentPlan.isBossWave)
            EventBus.FireBossWaveStart(currentWave);
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
}
