using System;
using UnityEngine;

// Oyuncu spawn ve revive yaşam döngüsü.
// Başlangıçta spawn noktasına yerleştirir ve canı doldurur.
// Ölünce hareket/savaş/etkileşim kontrolünü kapatır; ragdoll fiziğini RagdollController yönetir.
// EventBus.OnPlayerRevived kendi ID'miz için gelince: can full + kontrol geri verilir
// (ragdoll kapatma RagdollController'da). Revive yerinde olur (sela cesedi diriltir), ışınlama yok.
public class PlayerSpawnController : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerController movement;
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerInteraction interaction;

    [Header("Spawn")]
    [SerializeField] private Transform spawnPoint;          // boş ise başlangıç konumunda kalır

    [SerializeField] private int playerID = -1;             // Network katmanı atar (PlayerHealth ile aynı)

    private void Awake()
    {
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (movement == null) movement = GetComponent<PlayerController>();
        if (combat == null) combat = GetComponent<PlayerCombat>();
        if (interaction == null) interaction = GetComponent<PlayerInteraction>();
    }

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.OnDeath += HandleDeath;
        EventBus.OnPlayerRevived += HandleRevived;
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnDeath -= HandleDeath;
        EventBus.OnPlayerRevived -= HandleRevived;
    }

    private void Start()
    {
        if (spawnPoint != null)
            TeleportTo(spawnPoint.position, spawnPoint.rotation);

        if (playerHealth != null) playerHealth.ResetHealth();
        SetControlEnabled(true);
    }

    private void HandleDeath()
    {
        // Ceset yerde kalır (sela için); hareket/savaş kapanır, fizik RagdollController'da
        SetControlEnabled(false);
    }

    private void HandleRevived(int revivedID)
    {
        if (revivedID != playerID) return;

        if (playerHealth != null) playerHealth.ResetHealth();   // can full
        SetControlEnabled(true);                                // kontrol geri ver
    }

    // Hareket/savaş/etkileşim scriptlerini topluca aç/kapat
    private void SetControlEnabled(bool enabled)
    {
        if (movement != null) movement.enabled = enabled;
        if (combat != null) combat.enabled = enabled;
        if (interaction != null) interaction.enabled = enabled;
    }

    // CharacterController açıkken transform.position güvenilir değil; CC'yi kapatıp konumla, sonra geri aç
    private void TeleportTo(Vector3 position, Quaternion rotation)
    {
        bool wasEnabled = characterController != null && characterController.enabled;
        if (characterController != null) characterController.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        if (characterController != null) characterController.enabled = wasEnabled;
    }

    // Network katmanı atar — PlayerHealth.SetPlayerID ile aynı ID kullanılmalı.
    public void SetPlayerID(int id) => playerID = id;
}
