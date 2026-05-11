using System;
using UnityEngine;

// Oyun fazı kontrolcüsü — Prep ve Wave arasında geçiş yapar. Singleton.
// Prep süresi dolunca Wave fazına geçer; bir wave bittiğinde (OnWaveEnd) tekrar Prep'e döner.
// Prep süresi wave ilerledikçe kısalır: PREP_BASE_DURATION'dan PREP_MIN_DURATION'a kadar.
public class GamePhaseController : MonoBehaviour
{
    public static GamePhaseController Instance { get; private set; }

    [SerializeField] private float prepPhaseDuration = GameConstants.PREP_BASE_DURATION;
    [SerializeField] private float prepReductionPerWave = 5f;   // Her tamamlanan wave prep süresini bu kadar kısaltır

    private GamePhase currentPhase = GamePhase.Prep;
    private float prepTimer;
    private int lastCompletedWave;

    public GamePhase CurrentPhase => currentPhase;
    public float PrepTimeLeft => currentPhase == GamePhase.Prep ? prepTimer : 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        EventBus.OnWaveEnd += HandleWaveEnd;
    }

    private void OnDisable()
    {
        EventBus.OnWaveEnd -= HandleWaveEnd;
    }

    private void Start()
    {
        EnterPrep();
    }

    private void Update()
    {
        if (currentPhase != GamePhase.Prep) return;

        prepTimer -= Time.deltaTime;
        if (prepTimer <= 0f)
            EnterWave();
    }

    private void EnterPrep()
    {
        currentPhase = GamePhase.Prep;
        prepTimer = CurrentPrepDuration();
        EventBus.FirePhaseChanged(GamePhase.Prep);
    }

    private void EnterWave()
    {
        currentPhase = GamePhase.Wave;
        EventBus.FirePhaseChanged(GamePhase.Wave);
    }

    private void HandleWaveEnd(int waveNumber)
    {
        lastCompletedWave = waveNumber;
        EnterPrep();
    }

    // İleri wave'lerde daha kısa hazırlık süresi
    private float CurrentPrepDuration()
    {
        float reduced = prepPhaseDuration - prepReductionPerWave * lastCompletedWave;
        return Mathf.Max(GameConstants.PREP_MIN_DURATION, reduced);
    }
}
