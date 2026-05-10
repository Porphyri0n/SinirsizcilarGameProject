using System;
using UnityEngine;

// Gözetleme kulesindeki çan (IInteractable). Oyuncu "[E] Çan Çal" ile sinyal seçer ve çalar.
// Ses tüm haritaya yayılır (TOWER_VOICE_RANGE); seçilen sinyal EventBus.FireBellRung ile duyurulur.
public class BellSystem : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactPrompt = "[E] Çan Çal";
    [SerializeField] private BellSignal defaultSignal = BellSignal.EnemyApproaching;

    [Header("Efektler")]
    [SerializeField] private AudioSource bellAudio;
    [SerializeField] private AudioClip bellClip;
    [SerializeField] private ParticleSystem ringEffect;

    // Sinyal seçim menüsü isteği — bir UI dinler, seçimi yapıp RingBell çağırır.
    public event Action<GameObject> OnRingMenuRequested;

    private void Awake()
    {
        if (bellAudio != null)
        {
            bellAudio.spatialBlend = 1f;
            bellAudio.rolloffMode = AudioRolloffMode.Linear;
            bellAudio.minDistance = GameConstants.VOICE_FALLOFF_START;
            bellAudio.maxDistance = GameConstants.TOWER_VOICE_RANGE;
        }
    }

    // ── IInteractable ────────────────────────────────────────────────────
    public string GetInteractPrompt() => interactPrompt;
    public bool CanInteract(GameObject player) => true;

    public void Interact(GameObject player)
    {
        if (OnRingMenuRequested != null)
            OnRingMenuRequested.Invoke(player);
        else
            RingBell(defaultSignal);
    }

    // Çanı seçilen sinyalle çalar — ses + efekt + EventBus duyurusu.
    public void RingBell(BellSignal signal)
    {
        if (bellAudio != null && bellClip != null)
            bellAudio.PlayOneShot(bellClip);
        if (ringEffect != null)
            ringEffect.Play();

        EventBus.FireBellRung(signal);
    }
}
