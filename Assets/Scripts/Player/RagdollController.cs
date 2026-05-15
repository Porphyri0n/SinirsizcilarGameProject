using System;
using UnityEngine;

// Oyuncu ölünce ragdoll'a geçer: Animator kapanır, child Rigidbody'ler fiziğe açılır.
// Ceset yerde kalır (sela mekaniği için). EventBus.OnPlayerRevived bizim ID için
// gelince ragdoll kapanır ve oyuncu ayağa kalkar.
public class RagdollController : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Collider mainCollider;             // Ana karakter collider'ı (ragdoll değil)

    [SerializeField] private int playerID = -1;                 // Network katmanı atar, PlayerHealth ile aynı

    private Rigidbody[] ragdollBodies;
    private Collider[] ragdollColliders;

    public bool IsRagdollActive { get; private set; }

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        if (characterController == null) characterController = GetComponent<CharacterController>();

        ragdollBodies = GetComponentsInChildren<Rigidbody>(true);
        ragdollColliders = GetComponentsInChildren<Collider>(true);

        SetRagdollActive(false);        // Başlangıçta normal mod
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.OnDeath += HandleDeath;
        EventBus.OnPlayerRevived += HandleRevived;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnDeath -= HandleDeath;
        EventBus.OnPlayerRevived -= HandleRevived;
    }

    private void HandleDeath() => SetRagdollActive(true);

    private void HandleRevived(int revivedID)
    {
        if (revivedID != playerID) return;
        SetRagdollActive(false);
    }

    // true: ragdoll açık (Animator kapalı, fizik açık) | false: normal kontrol
    private void SetRagdollActive(bool active)
    {
        IsRagdollActive = active;

        if (animator != null) animator.enabled = !active;
        if (characterController != null) characterController.enabled = !active;
        if (mainCollider != null) mainCollider.enabled = !active;

        if (ragdollBodies != null)
        {
            foreach (Rigidbody rb in ragdollBodies)
            {
                if (rb == null) continue;
                rb.isKinematic = !active;
                rb.detectCollisions = active;
            }
        }

        if (ragdollColliders != null)
        {
            foreach (Collider c in ragdollColliders)
            {
                if (c == null || c == mainCollider) continue;
                c.enabled = active;
            }
        }
    }

    // Network katmanı (PlayerNetSync) atar — PlayerHealth.SetPlayerID ile aynı ID kullanılmalı.
    public void SetPlayerID(int id) => playerID = id;
}
