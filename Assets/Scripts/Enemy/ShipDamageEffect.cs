using System;
using UnityEngine;

// Geminin can yüzdesine göre hasar efektlerini açar:
//   <= %75 → hafif duman
//   <= %50 → yoğun duman
//   <= %25 → ateş
// ShipBase.OnHealthChanged'i dinler. Tüm ParticleSystem'ler Inspector'dan bağlanır.
[RequireComponent(typeof(ShipBase))]
public class ShipDamageEffect : MonoBehaviour
{
    [Header("Referans")]
    [SerializeField] private ShipBase ship;

    [Header("Efektler (ParticleSystem)")]
    [SerializeField] private ParticleSystem lightSmoke;     // %75 altında
    [SerializeField] private ParticleSystem heavySmoke;     // %50 altında
    [SerializeField] private ParticleSystem fire;           // %25 altında

    private const float LIGHT_THRESHOLD = 0.75f;
    private const float HEAVY_THRESHOLD = 0.50f;
    private const float FIRE_THRESHOLD = 0.25f;

    private void Reset()
    {
        ship = GetComponent<ShipBase>();
    }

    private void OnEnable()
    {
        if (ship == null)
            ship = GetComponent<ShipBase>();

        if (ship != null)
            ship.OnHealthChanged += HandleHealthChanged;

        // pool'dan tekrar kullanımda tüm efektleri kapat
        SetEffect(lightSmoke, false);
        SetEffect(heavySmoke, false);
        SetEffect(fire, false);
    }

    private void OnDisable()
    {
        if (ship != null)
            ship.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (max <= 0f) return;

        float pct = current / max;
        SetEffect(lightSmoke, pct <= LIGHT_THRESHOLD);
        SetEffect(heavySmoke, pct <= HEAVY_THRESHOLD);
        SetEffect(fire, pct <= FIRE_THRESHOLD);
    }

    private void SetEffect(ParticleSystem ps, bool play)
    {
        if (ps == null) return;

        if (play && !ps.isPlaying)
            ps.Play(true);
        else if (!play && ps.isPlaying)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
