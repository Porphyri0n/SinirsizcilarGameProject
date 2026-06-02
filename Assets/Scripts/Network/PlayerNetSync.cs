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

    private readonly NetworkVariable<Vector3> netPos = new NetworkVariable<Vector3>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<Quaternion> netRot = new NetworkVariable<Quaternion>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<bool> netMov = new NetworkVariable<bool>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<bool> netSpr = new NetworkVariable<bool>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<FixedString32Bytes> netCarry = new NetworkVariable<FixedString32Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private Vector3 netPosition;
    private Quaternion netRotation;
    private Vector3 netVelocity;
    private Vector3 lastReceivedPos;
    private float lastReceiveTime;
    private bool lastCarrying;

    private void Awake()
    {
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (carrySystem == null) carrySystem = GetComponent<CarrySystem>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        netPosition = transform.position;
        netRotation = transform.rotation;
        lastReceivedPos = transform.position;
        lastReceiveTime = Time.time;

        netPos.OnValueChanged += OnPositionChanged;
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
            // Owner: Oyun henüz başlamadıysa kontrolleri geçici olarak kapat
            bool gameStarted = false;
            if (GameStateSync.Instance != null && GameStateSync.Instance.IsSpawned)
            {
                gameStarted = GameStateSync.Instance.GameStarted.Value;
            }
            else if (GameNetworkManager.Instance != null)
            {
                gameStarted = GameNetworkManager.Instance.GameStarted;
            }
            else
            {
                gameStarted = true;
            }

            if (!gameStarted)
            {
                SetLocalControlsEnabled(false);
                if (GameStateSync.Instance != null)
                {
                    GameStateSync.Instance.GameStarted.OnValueChanged += HandleGameStartedChanged;
                    
                    // NGO 1.x OnValueChanged başlangıç değeri için tetiklenmeyebilir, 
                    // bu yüzden abonelikten hemen sonra tekrar kontrol ediyoruz.
                    if (GameStateSync.Instance.GameStarted.Value)
                    {
                        HandleGameStartedChanged(false, true);
                    }
                }
            }
            else
            {
                SetLocalControlsEnabled(true);
            }
}
    }

    public override void OnNetworkDespawn()
    {
        netPos.OnValueChanged -= OnPositionChanged;
        netCarry.OnValueChanged -= OnCarryChanged;

        // Proximity chat kaydını sil
        if (ProximityChatManager.Instance != null)
            ProximityChatManager.Instance.UnregisterPlayer((int)OwnerClientId);

        if (IsOwner && GameStateSync.Instance != null)
        {
            GameStateSync.Instance.GameStarted.OnValueChanged -= HandleGameStartedChanged;
        }
    }

    private void HandleGameStartedChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            SetLocalControlsEnabled(true);
            if (GameStateSync.Instance != null)
            {
                GameStateSync.Instance.GameStarted.OnValueChanged -= HandleGameStartedChanged;
            }
        }
    }

    private void SetLocalControlsEnabled(bool enabled)
    {
        if (playerController != null) playerController.enabled = enabled;

        PlayerCombat combat = GetComponent<PlayerCombat>();
        if (combat != null) combat.enabled = enabled;

        PlayerInteraction interaction = GetComponent<PlayerInteraction>();
        if (interaction != null) interaction.enabled = enabled;
    }

    private void Update()
    {
        if (IsOwner)
        {
            PushCarryState();
            netPos.Value = transform.position;
            netRot.Value = transform.rotation;
            netMov.Value = playerController != null && playerController.IsMoving;
            netSpr.Value = playerController != null && playerController.IsSprinting;
        }
        else
        {
            ApplyRemoteTransform();
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

    private void OnPositionChanged(Vector3 oldPos, Vector3 newPos)
    {
        if (IsOwner) return;

        float interval = Time.time - lastReceiveTime;
        if (lastReceiveTime > 0f && interval > 0f)
            netVelocity = (newPos - lastReceivedPos) / interval;
        lastReceivedPos = newPos;
        lastReceiveTime = Time.time;

        float lag = 0f;
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.NetworkConfig != null && Unity.Netcode.NetworkManager.Singleton.NetworkConfig.NetworkTransport != null)
        {
            lag = (float)Unity.Netcode.NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(OwnerClientId) / 2000f;
        }
        netPosition = newPos + netVelocity * Mathf.Clamp(lag, 0f, 0.5f);
        netRotation = netRot.Value;
    }

    private void ApplyRemoteTransform()
    {
        // Paketler arası hareketi tahmini hızla sürdür — hedefe yaklaşırken oluşan deselerasyon stutter'ını önler.
        if (netMov.Value)
            netPosition += netVelocity * Time.deltaTime;

        if ((transform.position - netPosition).sqrMagnitude > teleportDistance * teleportDistance)
            transform.position = netPosition;
        else
            transform.position = Vector3.Lerp(transform.position, netPosition, lerpSpeed * Time.deltaTime);

        transform.rotation = Quaternion.Slerp(transform.rotation, netRotation, lerpSpeed * Time.deltaTime);

        if (animator != null)
        {
            animator.SetBool(MovingHash, netMov.Value);
            animator.SetBool(SprintingHash, netSpr.Value);
        }
    }
}
