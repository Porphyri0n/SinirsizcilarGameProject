using System;
using System.Collections.Generic;
using UnityEngine;

// El arabası — oyun başından mevcut, craft gerektirmez. Rigidbody ile itilerek hareket eder.
// Kapasite ve hız WheelbarrowData SO'dan gelir; IUpgradeable ile Tier1→2→3 yükseltilebilir.
[RequireComponent(typeof(Rigidbody))]
public class WheelbarrowController : MonoBehaviour, IUpgradeable
{
    [Header("Veri")]
    [SerializeField] private WheelbarrowData[] levelData;       // Her UpgradeLevel için bir kayıt
    [SerializeField] private UpgradeData[] upgrades;            // fromLevel -> toLevel tanımları

    [Header("Fizik")]
    [SerializeField] private float followStrength = 8f;         // İtilince tutma noktasına yönelme gücü

    private Rigidbody rb;
    private Transform anchor;                                   // İten oyuncunun tutma noktası — null ise serbest
    private UpgradeLevel currentLevel = UpgradeLevel.Tier1;
    private readonly List<ICarriable> contents = new List<ICarriable>();

    public event Action<UpgradeLevel> OnUpgraded;

    public UpgradeLevel CurrentLevel => currentLevel;
    public int Capacity => CurrentData != null ? CurrentData.capacity : GameConstants.WHEELBARROW_BASE_CAPACITY;
    public float SpeedMultiplier => CurrentData != null ? CurrentData.speedMultiplier : GameConstants.WHEELBARROW_BASE_SPEED;
    public int ItemCount => contents.Count;
    public bool IsFull => contents.Count >= Capacity;
    public bool IsHeld => anchor != null;
    public IReadOnlyList<ICarriable> Contents => contents;

    private WheelbarrowData CurrentData
    {
        get
        {
            if (levelData == null) return null;
            foreach (WheelbarrowData d in levelData)
                if (d != null && d.level == currentLevel) return d;
            return null;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (anchor == null) return;

        // Tutma noktasına çek + bakış yönünü hizala, hız çarpanıyla ölçekli
        rb.velocity = (anchor.position - rb.position) * followStrength * SpeedMultiplier;

        Vector3 faceDir = anchor.forward;
        faceDir.y = 0f;
        if (faceDir.sqrMagnitude > 0.001f)
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(faceDir), followStrength * Time.fixedDeltaTime));
    }

    // ── Tutma ───────────────────────────────────────────────────────────
    public void Grab(Transform holdAnchor) => anchor = holdAnchor;
    public void Release() => anchor = null;

    // ── İçerik ──────────────────────────────────────────────────────────
    public bool CanAdd(ICarriable item) => item != null && !IsFull;

    public bool Add(ICarriable item)
    {
        if (!CanAdd(item)) return false;
        contents.Add(item);
        return true;
    }

    public List<ICarriable> UnloadAll()
    {
        List<ICarriable> taken = new List<ICarriable>(contents);
        contents.Clear();
        return taken;
    }

    // ── IUpgradeable ────────────────────────────────────────────────────
    public UpgradeData GetNextUpgrade()
    {
        if (upgrades == null) return null;
        foreach (UpgradeData u in upgrades)
            if (u != null && u.fromLevel == currentLevel) return u;
        return null;
    }

    public bool CanUpgrade() => GetNextUpgrade() != null;

    public void Upgrade()
    {
        UpgradeData next = GetNextUpgrade();
        if (next == null) return;

        currentLevel = next.toLevel;        // Yeni seviyenin WheelbarrowData'sı kapasite ve hızı otomatik günceller
        OnUpgraded?.Invoke(currentLevel);
    }
}
