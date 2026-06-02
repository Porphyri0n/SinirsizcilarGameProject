using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class PotionEffectUIToolkit : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private PotionData[] potions;
    [SerializeField] private float fadeDuration = 0.5f;

    private VisualElement _potionOverlay;
    private int _localPlayerId = -1;
    private Coroutine _effectRoutine;

    public void SetLocalPlayer(int playerId) => _localPlayerId = playerId;

    private void Awake()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            _potionOverlay = uiDocument.rootVisualElement.Q<VisualElement>("potionOverlay");
        }
        
        if (_potionOverlay != null)
        {
            SetAlpha(0f);
        }

        EventBus.OnPotionUsed += HandlePotionUsed;
    }

    private void OnDisable()
    {
        EventBus.OnPotionUsed -= HandlePotionUsed;
    }

    private void HandlePotionUsed(int playerId, PotionType type, float duration)
    {
        if (_localPlayerId >= 0 && playerId != _localPlayerId) return;
        if (_potionOverlay == null) return;

        PotionData data = FindPotion(type);
        if (data == null) return;

        if (_effectRoutine != null) StopCoroutine(_effectRoutine);
        _effectRoutine = StartCoroutine(EffectRoutine(data.screenTintColor, duration));
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
        _potionOverlay.style.backgroundColor = new Color(tint.r, tint.g, tint.b, 0f);

        float peak = tint.a;
        yield return Fade(0f, peak, tint);

        float hold = Mathf.Max(0f, duration - 2f * fadeDuration);
        yield return new WaitForSeconds(hold);

        yield return Fade(peak, 0f, tint);
        _effectRoutine = null;
    }

    private IEnumerator Fade(float from, float to, Color tint)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, t / fadeDuration);
            _potionOverlay.style.backgroundColor = new Color(tint.r, tint.g, tint.b, a);
            yield return null;
        }
        _potionOverlay.style.backgroundColor = new Color(tint.r, tint.g, tint.b, to);
    }

    private void SetAlpha(float a)
    {
        if (_potionOverlay == null) return;
        Color c = _potionOverlay.resolvedStyle.backgroundColor;
        _potionOverlay.style.backgroundColor = new Color(c.r, c.g, c.b, a);
    }
}
