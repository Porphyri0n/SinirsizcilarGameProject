using System;
using UnityEngine;
using Unity.Netcode;

// Wave ilerleyisini yoneten singleton.
// OnPhaseChanged(Wave) ile yeni wave baslatir; WaveScaler'dan prosedurel plan alir.
// OnShipDestroyed dinleyerek kalan gemi sayacini guncel tutar; sayac 0'a inince OnWaveEnd.
// Boss wave'de ayrica OnBossWaveStart firelar. Gemilerin spawn'i WaveSpawner'a (Erdo) ait.
public class WaveManager : NetworkBehaviour
{
    public static WaveManager Instance { get; private set; }

    [SerializeField] private int startingWave = 0;

    private int currentWave;
    private NetworkVariable<int> remainingShips = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private WavePlan currentPlan;
    private bool waveActive;
    private bool gameOver;   // Kale yıkıldıktan sonra yeni wave başlatmayı durdurur

    public int CurrentWave => currentWave;
    public int RemainingShips => remainingShips.Value;
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
        EventBus.OnWaveStart += HandleWaveStart;
        EventBus.OnWaveEnd += HandleWaveEnd;
        EventBus.OnShipDestroyed += HandleShipDestroyed;
        EventBus.OnGameLost += HandleGameLost;
        EventBus.OnGameRestart += HandleGameRestart;
    }

    private void OnDisable()
    {
        EventBus.OnPhaseChanged -= HandlePhaseChanged;
        EventBus.OnWaveStart -= HandleWaveStart;
        EventBus.OnWaveEnd -= HandleWaveEnd;
        EventBus.OnShipDestroyed -= HandleShipDestroyed;
        EventBus.OnGameLost -= HandleGameLost;
        EventBus.OnGameRestart -= HandleGameRestart;
    }

    private void HandleGameRestart()
    {
        currentWave = startingWave;
        if (IsServer) remainingShips.Value = 0;
        currentPlan = default;
        waveActive = false;
        gameOver = false;
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        // Sadece Host dalgayı başlatır. Clientlar GameStateSync üzerinden gelen OnWaveStart event'i ile initialize olur.
        if (phase == GamePhase.Wave && !waveActive && IsServer)
            StartNextWave();
    }

    private void HandleWaveStart(int waveNumber)
    {
        // Host zaten StartNextWave içinde her şeyi kurdu ve event'i ateşledi.
        // Clientlar burada kendi yerel verilerini (plan vb.) senkronize eder.
        // remainingShips artık NetworkVariable olduğu için manuel set etmeye gerek yok.
        if (!IsServer)
        {
            currentWave = waveNumber;
            currentPlan = WaveScaler.Plan(currentWave);
            waveActive = true;
            
            Debug.Log($"[WaveManager] Client synced to Wave {waveNumber}. Remaining: {remainingShips.Value}");
        }
    }

    private void HandleWaveEnd(int waveNumber)
    {
        waveActive = false;
    }

    // Bir sonraki wave'i baslatir: planlama + event yayinlama.
    public void StartNextWave()
    {
        if (gameOver || waveActive) return;   // oyun bittiyse / wave zaten aktifken yeni wave başlatma

        if (IsServer)
        {
            currentWave++;
            currentPlan = WaveScaler.Plan(currentWave);
            remainingShips.Value = currentPlan.TotalShips;

            if (remainingShips.Value <= 0)   // boş wave: sayaç hiç 0'a inemez, soft-lock olmasın diye hemen bitir
            {
                EventBus.FireWaveEnd(currentWave);
                return;
            }

            waveActive = true;
            EventBus.FireWaveStart(currentWave);
            if (currentPlan.isBossWave)
                EventBus.FireBossWaveStart(currentWave);
        }
    }

    // Wave'i zorla bitirir (Skip için).
    public void ForceEndWave()
    {
        if (!waveActive) return;
        
        if (IsServer)
        {
            waveActive = false;
            remainingShips.Value = 0;
            EventBus.FireWaveEnd(currentWave);
        }
    }

    private void HandleShipDestroyed(ShipType type, Vector3 pos)
    {
        if (!waveActive) return;

        // Sadece server sayacı günceller
        if (IsServer)
        {
            remainingShips.Value = Mathf.Max(0, remainingShips.Value - 1);
            if (remainingShips.Value == 0)
            {
                waveActive = false;
                EventBus.FireWaveEnd(currentWave);
            }
        }
    }

    private void HandleGameLost(int survivedWaves)
    {
        gameOver = true;
    }
}
