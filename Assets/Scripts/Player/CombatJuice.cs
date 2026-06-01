using System.Collections;
using UnityEngine;

public class CombatJuice : MonoBehaviour
{
    public static CombatJuice Instance { get; private set; }

    private float defaultTimeScale = 1f;
    private Coroutine hitStopCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public void HitStop(float duration, float timeScale = 0.05f)
    {
        if (duration <= 0f) return;
        if (hitStopCoroutine != null) StopCoroutine(hitStopCoroutine);
        hitStopCoroutine = StartCoroutine(HitStopRoutine(duration, timeScale));
    }

    private IEnumerator HitStopRoutine(float duration, float timeScale)
    {
        Time.timeScale = timeScale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = defaultTimeScale;
        hitStopCoroutine = null;
    }

    public void Shake(float duration, float magnitude)
    {
        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(duration, magnitude);
        }
    }
}
