using System;
using System.Collections;
using UnityEngine;

// Boss gemisi — her 5 wave'de bir gelen guclu varyant. ShipBase'den turer.
// Ekstra can (healthMultiplier ile carpilir), sahile varinca burst saldiri,
// yan yana zigzag hareket pateryi, batinca cevreye coklu loot dusurme.
public class BossShip : ShipBase
{
    [Header("Boss Ayarlari")]
    [SerializeField] private float healthMultiplier = 3f;
    [SerializeField] private int bonusLootCount = 4;
    [SerializeField] private LootType bonusLootType = LootType.Resource;
    [SerializeField] private float bonusLootRadius = 4f;

    [Header("Coklu Saldiri (sahilde)")]
    [SerializeField] private int attackBurstCount = 3;
    [SerializeField] private float burstInterval = 0.4f;
    [SerializeField] private float burstDamage = 15f;

    [Header("Zigzag Hareket")]
    [SerializeField] private float zigzagAmplitude = 2f;
    [SerializeField] private float zigzagFrequency = 0.5f;

    private ShipMovement movement;
    private float enableTime;

    // Tip her zaman Boss — shipData yanlis bile olsa dogru rapor edilsin
    public override ShipType Type => ShipType.Boss;

    // UI/efekt boss bar icin bu degeri okur (base.MaxHealth bar yuzdesini bozar)
    public float EffectiveMaxHealth => MaxHealth * Mathf.Max(1f, healthMultiplier);

    protected override void Awake()
    {
        base.Awake();
        movement = GetComponent<ShipMovement>();
        currentHealth = EffectiveMaxHealth;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        currentHealth = EffectiveMaxHealth;
        enableTime = Time.time;

        if (movement == null) movement = GetComponent<ShipMovement>();
        if (movement != null) movement.OnReachedShore += HandleReachedShore;
    }

    private void OnDisable()
    {
        if (movement != null) movement.OnReachedShore -= HandleReachedShore;
    }

    private void Update()
    {
        if (!IsAlive) return;
        if (movement != null && movement.HasArrived) return;

        // Yon vektorune dik eksende sinus kaydirma — zigzag
        float omega = zigzagFrequency * Mathf.PI * 2f;
        float lateral = Mathf.Sin((Time.time - enableTime) * omega) * zigzagAmplitude * Time.deltaTime;

        Vector3 forward = transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        transform.position += right * lateral;
    }

    private void HandleReachedShore()
    {
        StartCoroutine(AttackBurstRoutine());
    }

    private IEnumerator AttackBurstRoutine()
    {
        IDamageable castle = FindCastle();
        float wait = Mathf.Max(0.05f, burstInterval);

        for (int i = 0; i < attackBurstCount; i++)
        {
            if (!IsAlive) yield break;

            if (castle == null || !castle.IsAlive)
                castle = FindCastle();
            if (castle == null) yield break;

            castle.TakeDamage(burstDamage, transform.position);
            yield return new WaitForSeconds(wait);
        }
    }

    private IDamageable FindCastle()
    {
        GameObject go = GameObject.FindGameObjectWithTag(GameConstants.TAG_CASTLE);
        return go != null ? go.GetComponent<IDamageable>() : null;
    }

    // Batinca cevreye bonus loot — daire seklinde dagilir
    protected override void OnSink()
    {
        Vector3 center = transform.position;
        int count = Mathf.Max(1, bonusLootCount);

        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * bonusLootRadius;
            EventBus.FireLootDropped(center + offset, bonusLootType);
        }
    }
}
