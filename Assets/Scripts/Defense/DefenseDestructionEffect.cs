using System;
using UnityEngine;

// Sur / kule yikilinca calistirilan gorsel + ses geri bildirimi.
// Hedefe (Wall vb. IDamageable) abone olur; OnDeath'te particle burst, debris ve ses oynatir.
// Tamir edilirse efekt kendiliginden durur — sonraki yikilmada tekrar oynar.
public class DefenseDestructionEffect : MonoBehaviour
{
    [Header("Hedef")]
    [Tooltip("Yikildiginda efekt oynatilacak Wall (veya OnDeath fire eden baska bir komponent).")]
    [SerializeField] private Wall target;

    [Header("Gorsel")]
    [SerializeField] private ParticleSystem burstEffect;
    [SerializeField] private GameObject debrisPrefab;       // taş/talaş parçalari — fizikli prefab
    [SerializeField] private int debrisCount = 6;
    [SerializeField] private float debrisForce = 4f;
    [SerializeField] private float debrisLifetime = 5f;

    [Header("Ses")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip destructionClip;
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 0.9f;

    private bool played;   // tekrar tamir edilip yikilmadikca bir kere oynar

    private void Awake()
    {
        if (target == null) target = GetComponent<Wall>();
    }

    private void OnEnable()
    {
        if (target != null)
        {
            target.OnDeath += HandleDestroyed;
            target.OnHealthChanged += HandleHealthChanged;
        }
    }

    private void OnDisable()
    {
        if (target != null)
        {
            target.OnDeath -= HandleDestroyed;
            target.OnHealthChanged -= HandleHealthChanged;
        }
    }

    // Tamir edilip cana donerse efekti sifirla — sonraki yikilista yeniden oynasin.
    private void HandleHealthChanged(float current, float max)
    {
        if (current > 0f) played = false;
    }

    private void HandleDestroyed()
    {
        if (played) return;
        played = true;

        Vector3 pos = transform.position;

        if (burstEffect != null)
        {
            burstEffect.transform.position = pos;
            burstEffect.Play();
        }

        SpawnDebris(pos);

        if (sfxSource != null && destructionClip != null)
            sfxSource.PlayOneShot(destructionClip, sfxVolume);
    }

    private void SpawnDebris(Vector3 origin)
    {
        if (debrisPrefab == null || debrisCount <= 0) return;

        for (int i = 0; i < debrisCount; i++)
        {
            GameObject piece = Instantiate(debrisPrefab, origin, UnityEngine.Random.rotation);
            Rigidbody rb = piece.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 dir = (Vector3.up + UnityEngine.Random.insideUnitSphere * 0.6f).normalized;
                rb.AddForce(dir * debrisForce, ForceMode.Impulse);
            }
            Destroy(piece, debrisLifetime);
        }
    }
}
