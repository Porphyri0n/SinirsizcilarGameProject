using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// İksir ekran efekti — OnPotionUsed gelince PotionData.screenTintColor ile ekran kenarına
// renkli vinyet bindirir. Süre boyunca açık kalır, fade in/out ile girer ve çıkar.
// Vinyet, tam ekran kaplayan bir Image (kenarları renkli sprite) üzerinde renklenir.
public class PotionEffectUI : MonoBehaviour
{
    [SerializeField] private Image edgeOverlay;             // Tam ekran vinyet sprite'ı
    [SerializeField] private PotionData[] potions;          // PotionType -> screenTintColor eşlemesi
    [SerializeField] private float fadeDuration = 0.5f;

    // Yalnızca yerel oyuncunun iksiri ekranı boyasın. -1 ise (atanmamış) tüm pid'lere yanıt verir.
    private int localPlayerId = -1;
    private Coroutine effectRoutine;

    public void SetLocalPlayer(int playerId) => localPlayerId = playerId;

    private void Awake()
    {
        if (edgeOverlay != null)
        {
            edgeOverlay.raycastTarget = false;
            SetAlpha(0f);
        }
    }

    private void OnEnable()
    {
        EventBus.OnPotionUsed += HandlePotionUsed;
    }

    private void OnDisable()
    {
        EventBus.OnPotionUsed -= HandlePotionUsed;
    }

    private void HandlePotionUsed(int playerId, PotionType type, float duration)
    {
        if (localPlayerId >= 0 && playerId != localPlayerId) return;
        if (edgeOverlay == null) return;

        PotionData data = FindPotion(type);
        if (data == null) return;

        if (effectRoutine != null) StopCoroutine(effectRoutine);
        effectRoutine = StartCoroutine(EffectRoutine(data.screenTintColor, duration));
    }

    private PotionData FindPotion(PotionType type)
    {
        if (potions == null) return null;
        foreach (PotionData p in potions)
            if (p != null && p.potionType == type) return p;
        return null;
    }

    private IEnumerator EffectRoutine(Color tint, float duration)
    {
        edgeOverlay.color = new Color(tint.r, tint.g, tint.b, 0f);

        float peak = tint.a;                                // SO'daki rengin alpha'sı tepe yoğunluk
        yield return Fade(0f, peak, tint);

        // Fade in/out süresini toplam süreden düş, kalanı açık tut
        float hold = Mathf.Max(0f, duration - 2f * fadeDuration);
        yield return new WaitForSeconds(hold);

        yield return Fade(peak, 0f, tint);
        effectRoutine = null;
    }

    private IEnumerator Fade(float from, float to, Color tint)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, t / fadeDuration);
            edgeOverlay.color = new Color(tint.r, tint.g, tint.b, a);
            yield return null;
        }
        edgeOverlay.color = new Color(tint.r, tint.g, tint.b, to);
    }

    private void SetAlpha(float a)
    {
        Color c = edgeOverlay.color;
        edgeOverlay.color = new Color(c.r, c.g, c.b, a);
    }
}
