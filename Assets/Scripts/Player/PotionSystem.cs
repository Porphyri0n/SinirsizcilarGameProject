using System;
using System.Collections;
using UnityEngine;

// İksir kullanımı — Strength (hasar boost) ve Hearing (ses menzili boost).
// Coroutine ile süre sayar; bitince çarpan 1'e döner.
// Aynı türden ikinci iksir gelirse süreyi sıfırdan başlatır.
// Çarpanları PlayerCombat ve ProximityChatManager dışarıdan okur.
public class PotionSystem : MonoBehaviour
{
    [SerializeField] private int playerID = -1;

    private Coroutine strengthRoutine;
    private Coroutine hearingRoutine;

    private float strengthMultiplier = 1f;
    private float hearingMultiplier = 1f;
    private float strengthRemaining;
    private float hearingRemaining;

    public float StrengthMultiplier => strengthMultiplier;
    public float HearingMultiplier => hearingMultiplier;
    public bool StrengthActive => strengthRoutine != null;
    public bool HearingActive => hearingRoutine != null;
    public float StrengthRemaining => strengthRemaining;
    public float HearingRemaining => hearingRemaining;

    public event Action<PotionType, float> OnPotionStarted;     // type, duration
    public event Action<PotionType> OnPotionEnded;

    // Inspector / network atayabilir.
    public void SetPlayerID(int id) => playerID = id;

    // İksir kullan. data null'sa hiçbir şey yapma. Aynı tür aktifse süreyi reset eder.
    public void UsePotion(PotionData data)
    {
        if (data == null) return;

        float duration = ResolveDuration(data);
        float value = ResolveEffectValue(data);

        EventBus.FirePotionUsed(playerID, data.potionType, duration);
        OnPotionStarted?.Invoke(data.potionType, duration);

        if (data.potionType == PotionType.Strength)
            RestartRoutine(ref strengthRoutine, StrengthRoutine(value, duration));
        else if (data.potionType == PotionType.Hearing)
            RestartRoutine(ref hearingRoutine, HearingRoutine(value, duration));
    }

    private float ResolveDuration(PotionData data)
    {
        if (data.duration > 0f) return data.duration;
        return data.potionType == PotionType.Strength
            ? GameConstants.STRENGTH_POTION_DURATION
            : GameConstants.HEARING_POTION_DURATION;
    }

    private float ResolveEffectValue(PotionData data)
    {
        if (data.effectValue > 0f) return data.effectValue;
        return data.potionType == PotionType.Strength
            ? GameConstants.STRENGTH_MULTIPLIER
            : GameConstants.HEARING_RANGE_MULTIPLIER;
    }

    private void RestartRoutine(ref Coroutine handle, IEnumerator routine)
    {
        if (handle != null) StopCoroutine(handle);
        handle = StartCoroutine(routine);
    }

    private IEnumerator StrengthRoutine(float value, float duration)
    {
        strengthMultiplier = value;
        strengthRemaining = duration;

        while (strengthRemaining > 0f)
        {
            strengthRemaining -= Time.deltaTime;
            yield return null;
        }

        strengthMultiplier = 1f;
        strengthRemaining = 0f;
        strengthRoutine = null;
        OnPotionEnded?.Invoke(PotionType.Strength);
    }

    private IEnumerator HearingRoutine(float value, float duration)
    {
        hearingMultiplier = value;
        hearingRemaining = duration;

        while (hearingRemaining > 0f)
        {
            hearingRemaining -= Time.deltaTime;
            yield return null;
        }

        hearingMultiplier = 1f;
        hearingRemaining = 0f;
        hearingRoutine = null;
        OnPotionEnded?.Invoke(PotionType.Hearing);
    }

    private void OnDisable()
    {
        // Sahne kapanışı ya da obje devre dışı kalınca buff sıfırlansın
        if (strengthRoutine != null) { StopCoroutine(strengthRoutine); strengthRoutine = null; }
        if (hearingRoutine != null) { StopCoroutine(hearingRoutine); hearingRoutine = null; }
        strengthMultiplier = 1f;
        hearingMultiplier = 1f;
        strengthRemaining = 0f;
        hearingRemaining = 0f;
    }
}
