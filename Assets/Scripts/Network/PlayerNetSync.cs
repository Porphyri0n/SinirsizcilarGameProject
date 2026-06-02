using System;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

// Oyuncu senkronu — transform, animasyon ve taşıma durumu.
// Sahip (IsMine): kendi pozisyon/rotasyonunu stream'e yazar, taşıma durumunu player property'sine basar.
// Diğer client'lar: gelen değere NETWORK_SYNC_RATE aralıklarla lerp eder, animator parametrelerini set eder.
public class PlayerNetSync : NetworkBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private CarrySystem carrySystem;
    [SerializeField] private float lerpSpeed = 12f;
    [SerializeField] private float teleportDistance = 5f;    // Bu mesafeden uzaksa lerp yerine ışınla

    private static readonly int MovingHash = Animator.StringToHash("IsMoving");
    private static readonly int SprintingHash = Animator.StringToHash("IsSprinting");
    private static readonly int CarryingHash = Animator.StringToHash("IsCarrying");

    private readonly NetworkVariable<bool> netMov = new NetworkVariable<bool>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<bool> netSpr = new NetworkVariable<bool>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<FixedString32Bytes> netCarry = new NetworkVariable<FixedString32Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private bool lastCarrying;

    private void Awake()
    {
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (carrySystem == null) carrySystem = GetComponent<CarrySystem>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        netCarry.OnValueChanged += OnCarryChanged;

        // Proximity chat: tüm oyuncuları (hem local hem remote) kaydet
        if (ProximityChatManager.Instance != null)
            ProximityChatManager.Instance.RegisterPlayer((int)OwnerClientId, transform);

        if (!IsOwner)
        {
            ApplyCarryState(netCarry.Value.ToString());

            // Diğer oyuncuların (remote) yerel girdi ve kontrol bileşenlerini kapat
            if (playerController != null) playerController.enabled = false;

            PlayerCombat combat = GetComponent<PlayerCombat>();
            if (combat != null) combat.enabled = false;

            PlayerInteraction interaction = GetComponent<PlayerInteraction>();
            if (interaction != null) interaction.enabled = false;

            PlayerSpawnController spawnController = GetComponent<PlayerSpawnController>();
            if (spawnController != null) spawnController.enabled = false;

            // Kamera ve AudioListener'ı kapat (birden fazla kameranın çakışmaması için)
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cam.enabled = false;

            AudioListener listener = GetComponentInChildren<AudioListener>();
            if (listener != null) listener.enabled = false;
        }
        else
        {
            // Owner: Kontrol yönetimi PlayerSpawnController tarafından yapılır.
            // Burada sadece başlangıç durumunu koruyoruz.
        }
    }

    public override void OnNetworkDespawn()
    {
        netCarry.OnValueChanged -= OnCarryChanged;

        // Proximity chat kaydını sil
        if (ProximityChatManager.Instance != null)
            ProximityChatManager.Instance.UnregisterPlayer((int)OwnerClientId);
    }

    private void Update()
    {
        if (IsOwner)
        {
            PushCarryState();
            netMov.Value = playerController != null && playerController.IsMoving;
            netSpr.Value = playerController != null && playerController.IsSprinting;
        }
        else
        {
            ApplyRemoteAnimations();
        }
    }

    // ── Sahip tarafı ────────────────────────────────────────────────────

    private void PushCarryState()
    {
        bool carrying = carrySystem != null && carrySystem.IsCarrying;
        if (carrying == lastCarrying) return;

        lastCarrying = carrying;
        string itemName = carrying ? carrySystem.Carried.ItemName : string.Empty;
        netCarry.Value = itemName;
        if (animator != null) animator.SetBool(CarryingHash, carrying);
    }

    // ── Diğer client'lar ────────────────────────────────────────────────

    private void OnCarryChanged(FixedString32Bytes oldVal, FixedString32Bytes newVal)
    {
        if (IsOwner) return;
        ApplyCarryState(newVal.ToString());
    }

    private void ApplyCarryState(string itemName)
    {
        if (animator != null) animator.SetBool(CarryingHash, !string.IsNullOrEmpty(itemName));
    }

    private void ApplyRemoteAnimations()
    {
        if (animator != null)
        {
            animator.SetBool(MovingHash, netMov.Value);
            animator.SetBool(SprintingHash, netSpr.Value);
        }
    }
}
