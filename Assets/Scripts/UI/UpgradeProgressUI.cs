using System;
using System.Collections;
using UnityEngine;
using TMPro;

// Yükseltme bildirimi — OnUpgradeCompleted gelince "X → TierN" kısa bir bildirim gösterir.
// Fade in → bekle → fade out (klasik HUD yok, bildirim diegetic bir panel/3D text üzerinde belirir).
public class UpgradeProgressUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private float holdDuration = 2f;

    private Coroutine routine;

    private void Awake()
    {
        if (group != null) group.alpha = 0f;
    }

    private void OnEnable()
    {
        EventBus.OnUpgradeCompleted += HandleUpgradeCompleted;
    }

    private void OnDisable()
    {
        EventBus.OnUpgradeCompleted -= HandleUpgradeCompleted;
    }

    private void HandleUpgradeCompleted(string target, UpgradeLevel level)
    {
        if (label == null || group == null) return;

        label.text = $"{target} → {level}";
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(NotifyRoutine());
    }

    private IEnumerator NotifyRoutine()
    {
        yield return Fade(0f, 1f);
        yield return new WaitForSeconds(holdDuration);
        yield return Fade(1f, 0f);
        routine = null;
    }

    private IEnumerator Fade(float from, float to)
    {
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
