using System;
using UnityEngine;

// Wave / boss baslangicinda kamerayi kisa sureli titretir.
// OnWaveStart -> hafif shake, OnBossWaveStart -> guclu shake.
// Diger sistemler de Instance.Shake(duration, magnitude) ile cagirabilir
// (orn. kale hasari, top patlama, kule yikilmasi).
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Hedef")]
    [Tooltip("Sallanacak transform — bos birakilirsa Camera.main kullanilir.")]
    [SerializeField] private Transform targetTransform;

    [Header("Wave Sarsintilari")]
    [SerializeField] private float waveShakeDuration = 0.45f;
    [SerializeField] private float waveShakeMagnitude = 0.18f;
    [SerializeField] private float bossShakeDuration = 1.1f;
    [SerializeField] private float bossShakeMagnitude = 0.45f;

    [Header("Genel")]
    [Tooltip("Perlin noise hizi — daha yuksek = daha gergin titreme.")]
    [SerializeField] private float frequency = 22f;

    private float shakeTimer;
    private float shakeMagnitude;
    private float shakeDuration;
    private float noiseSeedX;
    private float noiseSeedY;

    private PlayerController localPlayerController;
    private Vector3 lastShakeOffset;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        if (targetTransform == null && Camera.main != null)
            targetTransform = Camera.main.transform;
    }

    private void OnEnable()
    {
        EventBus.OnWaveStart += HandleWaveStart;
        EventBus.OnBossWaveStart += HandleBossWaveStart;
    }

    private void OnDisable()
    {
        EventBus.OnWaveStart -= HandleWaveStart;
        EventBus.OnBossWaveStart -= HandleBossWaveStart;
    }

    private void LateUpdate()
    {
        if (targetTransform == null) return;

        Vector3 offset = Vector3.zero;
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            float k = Mathf.Clamp01(shakeTimer / shakeDuration);   // 1 -> 0 (lineer sonlanma)
            float amount = shakeMagnitude * k;

            // Perlin noise -1..1 araligina cevir, frekansla kayar (sabit seed = her shake farkli desen)
            float t = Time.time * frequency;
            float x = (Mathf.PerlinNoise(noiseSeedX, t) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(noiseSeedY, t) - 0.5f) * 2f;

            offset = targetTransform.right * x * amount + targetTransform.up * y * amount;
        }

        if (localPlayerController == null)
        {
            PlayerController[] controllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var c in controllers)
            {
                if (c.IsOwner)
                {
                    localPlayerController = c;
                    break;
                }
            }
        }

        bool isPlayerCamera = localPlayerController != null && localPlayerController.enabled;

        if (isPlayerCamera)
        {
            targetTransform.position += offset;
        }
        else
        {
            targetTransform.position -= lastShakeOffset;
            targetTransform.position += offset;
            lastShakeOffset = offset;
        }

        if (shakeTimer <= 0f)
        {
            lastShakeOffset = Vector3.zero;
        }
    }

    // Disaridan da cagrilabilir — orn. kale hasari, top patlama.
    public void Shake(float duration, float magnitude)
    {
        if (duration <= 0f || magnitude <= 0f) return;

        if (Camera.main != null)
            targetTransform = Camera.main.transform;

        if (targetTransform == null) return;

        // Daha buyuk bir shake aktifse onu ezme
        if (shakeTimer > 0f && magnitude < shakeMagnitude && shakeTimer > duration * 0.5f)
            return;

        shakeDuration = duration;
        shakeTimer = duration;
        shakeMagnitude = magnitude;
        noiseSeedX = UnityEngine.Random.value * 100f;
        noiseSeedY = UnityEngine.Random.value * 100f;
    }

    private void HandleWaveStart(int waveNumber) => Shake(waveShakeDuration, waveShakeMagnitude);
    private void HandleBossWaveStart(int waveNumber) => Shake(bossShakeDuration, bossShakeMagnitude);
}
