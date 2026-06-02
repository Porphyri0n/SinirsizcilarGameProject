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
    private Coroutine speedRoutine;
    private Coroutine regenerationRoutine;

    private float strengthMultiplier = 1f;
    private float hearingMultiplier = 1f;
    private float speedMultiplier = 1f;
    private float strengthRemaining;
    private float hearingRemaining;
    private float speedRemaining;
    private float regenerationRemaining;

    public float StrengthMultiplier => strengthMultiplier;
    public float HearingMultiplier => hearingMultiplier;
    public float SpeedMultiplier => speedMultiplier;
    public bool StrengthActive => strengthRoutine != null;
    public bool HearingActive => hearingRoutine != null;
    public bool SpeedActive => speedRoutine != null;
    public bool RegenerationActive => regenerationRoutine != null;
    public float StrengthRemaining => strengthRemaining;
    public float HearingRemaining => hearingRemaining;
    public float SpeedRemaining => speedRemaining;
    public float RegenerationRemaining => regenerationRemaining;

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
        else if (data.potionType == PotionType.Speed)
            RestartRoutine(ref speedRoutine, SpeedRoutine(value, duration));
        else if (data.potionType == PotionType.Regeneration)
            RestartRoutine(ref regenerationRoutine, RegenerationRoutine(value, duration));
    }

    private float ResolveDuration(PotionData data)
    {
        if (data.duration > 0f) return data.duration;
        switch (data.potionType)
        {
            case PotionType.Strength: return GameConstants.STRENGTH_POTION_DURATION;
            case PotionType.Hearing: return GameConstants.HEARING_POTION_DURATION;
            case PotionType.Speed: return 30f;
            case PotionType.Regeneration: return 20f;
            default: return 30f;
        }
    }

    private float ResolveEffectValue(PotionData data)
    {
        if (data.effectValue > 0f) return data.effectValue;
        switch (data.potionType)
        {
            case PotionType.Strength: return GameConstants.STRENGTH_MULTIPLIER;
            case PotionType.Hearing: return GameConstants.HEARING_RANGE_MULTIPLIER;
            case PotionType.Speed: return 1.5f;
            case PotionType.Regeneration: return 5f;
            default: return 1f;
        }
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

    private IEnumerator SpeedRoutine(float value, float duration)
    {
        speedMultiplier = value;
        speedRemaining = duration;

        while (speedRemaining > 0f)
        {
            speedRemaining -= Time.deltaTime;
            yield return null;
        }

        speedMultiplier = 1f;
        speedRemaining = 0f;
        speedRoutine = null;
        OnPotionEnded?.Invoke(PotionType.Speed);
    }

    private IEnumerator RegenerationRoutine(float value, float duration)
    {
        regenerationRemaining = duration;
        PlayerHealth health = GetComponent<PlayerHealth>();

        float timer = 0f;
        while (regenerationRemaining > 0f)
        {
            regenerationRemaining -= Time.deltaTime;
            timer += Time.deltaTime;
            if (timer >= 1f)
            {
                timer -= 1f;
                if (health != null && health.IsAlive)
                {
                    health.RequestHealServerRpc(value);
                }
            }
            yield return null;
        }

        regenerationRemaining = 0f;
        regenerationRoutine = null;
        OnPotionEnded?.Invoke(PotionType.Regeneration);
    }

    private void OnDisable()
    {
        // Sahne kapanışı ya da obje devre dışı kalınca buff sıfırlansın
        if (strengthRoutine != null) { StopCoroutine(strengthRoutine); strengthRoutine = null; }
        if (hearingRoutine != null) { StopCoroutine(hearingRoutine); hearingRoutine = null; }
        if (speedRoutine != null) { StopCoroutine(speedRoutine); speedRoutine = null; }
        if (regenerationRoutine != null) { StopCoroutine(regenerationRoutine); regenerationRoutine = null; }
        strengthMultiplier = 1f;
        hearingMultiplier = 1f;
        speedMultiplier = 1f;
        strengthRemaining = 0f;
        hearingRemaining = 0f;
        speedRemaining = 0f;
        regenerationRemaining = 0f;
    }
}
