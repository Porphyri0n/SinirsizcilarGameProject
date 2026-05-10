using System;
using System.Collections;
using UnityEngine;
using TMPro;

// Faz değişimi ve boss dalgası duyurusu — dünya içi yazı, fade in -> bekle -> fade out.
public class PhaseAnnouncerUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private float fadeDuration = 0.6f;
    [SerializeField] private float holdDuration = 2f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color bossColor = new Color(1f, 0.2f, 0.1f);

    private Coroutine routine;

    private void Awake()
    {
        if (group != null) group.alpha = 0f;
    }

    private void OnEnable()
    {
        EventBus.OnPhaseChanged += HandlePhaseChanged;
        EventBus.OnBossWaveStart += HandleBossWaveStart;
    }

    private void OnDisable()
    {
        EventBus.OnPhaseChanged -= HandlePhaseChanged;
        EventBus.OnBossWaveStart -= HandleBossWaveStart;
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        Show(phase == GamePhase.Prep ? "HAZIRLIK AŞAMASI" : "DALGA BAŞLIYOR", normalColor);
    }

    private void HandleBossWaveStart(int waveNumber)
    {
        Show("BOSS DALGASI!", bossColor);
    }

    private void Show(string text, Color color)
    {
        if (label != null)
        {
            label.text = text;
            label.color = color;
        }
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(AnnounceRoutine());
    }

    private IEnumerator AnnounceRoutine()
    {
        yield return Fade(0f, 1f);
        yield return new WaitForSeconds(holdDuration);
        yield return Fade(1f, 0f);
        routine = null;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (group == null) yield break;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        group.alpha = to;
    }
}
