using System;
using UnityEngine;

// Kervandan kaynak teslim alindiginda gorsel + ses geri bildirimi.
// OnResourceReceived dinler; particle burst calistirir, varsa ses oynatir
// ve floating text icin OnDelivered'i tetikler (UI dinleyebilir).
public class ResourceDeliveryEffect : MonoBehaviour
{
    [Header("Gorsel")]
    [SerializeField] private ParticleSystem burstEffect;
    [SerializeField] private Transform spawnAnchor;          // null ise bu transform kullanilir
    [SerializeField] private float burstScale = 1f;

    [Header("Ses")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip deliveryClip;
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 0.8f;

    // UI veya baska sistem floating "+5 Wood" yazisi gostermek isterse abone olabilir.
    public event Action<ResourceType, int, Vector3> OnDelivered;

    private void OnEnable()
    {
        EventBus.OnResourceReceived += HandleResourceReceived;
    }

    private void OnDisable()
    {
        EventBus.OnResourceReceived -= HandleResourceReceived;
    }

    private void HandleResourceReceived(ResourceType type, int amount)
    {
        Vector3 pos = spawnAnchor != null ? spawnAnchor.position : transform.position;

        if (burstEffect != null)
        {
            burstEffect.transform.position = pos;
            burstEffect.transform.localScale = Vector3.one * burstScale;
            burstEffect.Play();
        }

        if (sfxSource != null && deliveryClip != null)
            sfxSource.PlayOneShot(deliveryClip, sfxVolume);

        OnDelivered?.Invoke(type, amount, pos);
    }
}
