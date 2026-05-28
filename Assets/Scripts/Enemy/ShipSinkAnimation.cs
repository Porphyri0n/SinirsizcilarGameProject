using System;
using System.Collections;
using UnityEngine;

// Gemi can'i bittiginde calistirilan batma animasyonu.
// ShipBase veya ShipHealth'in OnDeath event'ine abone olur; coroutine ile gemiyi
// asagi cek + hafif yatir, ayni anda splash particle + ses oynat. Pool'a donduyse
// (OnEnable) durum sifirlanir; yeni hayatinda animasyon baska bir batma'ya hazir.
public class ShipSinkAnimation : MonoBehaviour
{
    [Header("Hedef (biri yeterli)")]
    [SerializeField] private ShipBase shipBase;
    [SerializeField] private ShipHealth shipHealth;

    [Header("Hareket")]
    [SerializeField] private float sinkDuration = 3f;
    [SerializeField] private float sinkDepth = 4f;
    [SerializeField] private float maxTiltAngle = 25f;

    [Header("Splash")]
    [SerializeField] private ParticleSystem splashEffect;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip splashClip;
    [Range(0f, 1f)]
    [SerializeField] private float splashVolume = 0.8f;

    private Vector3 startLocalPos;
    private Quaternion startLocalRot;
    private Coroutine sinkRoutine;

    private void Awake()
    {
        if (shipBase == null) shipBase = GetComponent<ShipBase>();
        if (shipHealth == null) shipHealth = GetComponent<ShipHealth>();
        startLocalPos = transform.localPosition;
        startLocalRot = transform.localRotation;
    }

    private void OnEnable()
    {
        // Pool'dan yeniden cikislarda pozisyon/rotasyon temiz baslar
        transform.localPosition = startLocalPos;
        transform.localRotation = startLocalRot;

        if (shipBase != null) shipBase.OnDeath += BeginSink;
        if (shipHealth != null) shipHealth.OnDeath += BeginSink;
    }

    private void OnDisable()
    {
        if (shipBase != null) shipBase.OnDeath -= BeginSink;
        if (shipHealth != null) shipHealth.OnDeath -= BeginSink;
        if (sinkRoutine != null) { StopCoroutine(sinkRoutine); sinkRoutine = null; }
    }

    private void BeginSink()
    {
        if (splashEffect != null)
        {
            splashEffect.transform.position = transform.position;
            splashEffect.Play();
        }
        if (sfxSource != null && splashClip != null)
            sfxSource.PlayOneShot(splashClip, splashVolume);

        if (sinkRoutine != null) StopCoroutine(sinkRoutine);
        sinkRoutine = StartCoroutine(SinkRoutine());
    }

    private IEnumerator SinkRoutine()
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.down * sinkDepth;

        // Rastgele yana yatma — her gemi farkli batsin
        float tiltX = UnityEngine.Random.Range(-maxTiltAngle, maxTiltAngle);
        float tiltZ = UnityEngine.Random.Range(-maxTiltAngle, maxTiltAngle);
        Quaternion startRot = transform.rotation;
        Quaternion endRot = startRot * Quaternion.Euler(tiltX, 0f, tiltZ);

        float elapsed = 0f;
        while (elapsed < sinkDuration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / sinkDuration);
            // ease-in: basta yavas, sonra hizlan — su altinda kaybolma hissi
            float eased = k * k;
            transform.position = Vector3.Lerp(startPos, endPos, eased);
            transform.rotation = Quaternion.Slerp(startRot, endRot, k);
            yield return null;
        }

        sinkRoutine = null;
    }
}
