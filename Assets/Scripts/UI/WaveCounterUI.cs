using System;
using UnityEngine;
using TMPro;

// Dünya içi (diegetic) dalga sayacı — sur duvarındaki tahta/panel.
// Mevcut dalga numarasını gösterir; bir sonraki dalga boss ise uyarı yazar.
// DiegeticUIManager'a kayıt olur, mesafeye göre görünürlüğü yönetilir.
public class WaveCounterUI : MonoBehaviour, IDiegeticUI
{
    [Header("Görsel")]
    [SerializeField] private TMP_Text waveLabel;
    [SerializeField] private TMP_Text bossWarningLabel;
    [SerializeField] private GameObject bossWarningRoot;        // Uyarı paneli — sıradaki boss ise açılır

    [Header("Diegetic")]
    [SerializeField] private Transform anchor;
    [SerializeField] private float visibleRange = 25f;

    private int currentWave;

    public Transform Anchor => anchor != null ? anchor : transform;
    public float VisibleRange => visibleRange;

    private void OnEnable()
    {
        EventBus.OnWaveStart += HandleWaveStart;
        EventBus.OnWaveEnd += HandleWaveEnd;
        EventBus.OnBossWaveStart += HandleBossWaveStart;

        if (DiegeticUIManager.Instance != null)
            DiegeticUIManager.Instance.Register(this);

        Refresh();
    }

    private void OnDisable()
    {
        EventBus.OnWaveStart -= HandleWaveStart;
        EventBus.OnWaveEnd -= HandleWaveEnd;
        EventBus.OnBossWaveStart -= HandleBossWaveStart;

        if (DiegeticUIManager.Instance != null)
            DiegeticUIManager.Instance.Unregister(this);
    }

    private void HandleWaveStart(int waveNumber)
    {
        currentWave = waveNumber;
        Refresh();
    }

    // Wave bittiğinde panel sıradakini öne çıkarsın diye yenile
    private void HandleWaveEnd(int waveNumber) => Refresh();

    private void HandleBossWaveStart(int waveNumber)
    {
        currentWave = waveNumber;
        Refresh();
    }

    private void Refresh()
    {
        if (waveLabel != null)
            waveLabel.text = $"DALGA {currentWave}";

        bool nextIsBoss = IsBossWave(currentWave + 1);
        if (bossWarningRoot != null)
            bossWarningRoot.SetActive(nextIsBoss);
        if (bossWarningLabel != null && nextIsBoss)
            bossWarningLabel.text = $"BOSS YAKLAŞIYOR — DALGA {currentWave + 1}";
    }

    private static bool IsBossWave(int waveNumber)
    {
        if (waveNumber <= 0) return false;
        return waveNumber % GameConstants.BOSS_WAVE_INTERVAL == 0;
    }

    // ── IDiegeticUI ──────────────────────────────────────────────────────
    public void SetVisibility(bool visible)
    {
        if (waveLabel != null) waveLabel.enabled = visible;
        if (bossWarningRoot != null)
            bossWarningRoot.SetActive(visible && IsBossWave(currentWave + 1));
    }
}
