using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Hasar yön göstergesi — yerel oyuncu vurulunca hasarın geldiği yöne dönük kırmızı bir
// vinyet belirir ve fade out olur (klasik can barı yok, yön bilgisi diegetic verilir).
// EventBus'ta yerel-hasar event'i yok; IDamageable.TakeDamage'daki hitPoint combat/network
// katmanından ShowDamageFrom(...) ile iletilir. Yön, kamera ekran açısından hesaplanır.
public class DamageIndicatorUI : MonoBehaviour
{
    [SerializeField] private RectTransform vignette;        // kırmızı yön vinyeti (merkez anchor, döner)
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Camera viewCamera;             // boş ise Camera.main
    [SerializeField] private float showDuration = 1f;
    [SerializeField] private float maxAlpha = 0.8f;

    private Coroutine routine;

    private void Awake()
    {
        if (group != null) group.alpha = 0f;
    }

    // Combat/network katmanı yerel oyuncu vurulunca çağırır (source = vuruşun geldiği dünya konumu).
    public void ShowDamageFrom(Vector3 sourceWorldPosition)
    {
        if (vignette == null || group == null) return;

        RotateToward(sourceWorldPosition);
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(FadeRoutine());
    }

    // Kaynağın ekran yönüne göre vinyeti döndür (BanditRaidUI ok hesabıyla aynı yaklaşım).
    private void RotateToward(Vector3 source)
    {
        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(source);
        Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;

        // Kamera arkasındaysa ekran yönünü ters çevir
        if (screenPos.z < 0f)
        {
            screenPos.x = Screen.width - screenPos.x;
            screenPos.y = Screen.height - screenPos.y;
        }

        Vector2 dir = (Vector2)screenPos - center;
        if (dir.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        vignette.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);     // vinyetin parlak ucu kaynağa döner
    }

    private IEnumerator FadeRoutine()
    {
        group.alpha = maxAlpha;

        float t = 0f;
        while (t < showDuration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(maxAlpha, 0f, t / showDuration);
            yield return null;
        }

        group.alpha = 0f;
        routine = null;
    }
}
