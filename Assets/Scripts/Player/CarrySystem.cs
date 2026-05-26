using System;
using UnityEngine;

// Oyuncunun tek eşya taşıma sistemi (ICarriable). Taşırken hız CARRY_SPEED_MULTIPLIER ile yavaşlar.
// Drop ederken önümüzde bir etkileşim hedefi (craft ocağı) varsa oraya teslim eder, yoksa yere bırakır.
// Ağır eşya (CarryWeight.Heavy) elle taşınamaz — el arabasıyla taşınır.
[RequireComponent(typeof(PlayerController))]
public class CarrySystem : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInteraction interaction;   // teslim hedefini (craft ocağı) bulmak için
    [SerializeField] private PlayerHealth health;             // ölünce taşınan eşyayı bırakmak için
    [SerializeField] private Transform holdPoint;             // Eşya taşınırken buraya tutturulur
    [SerializeField] private float dropForwardDistance = 1f;

    private ICarriable carried;

    public bool IsCarrying => carried != null;
    public ICarriable Carried => carried;
    public Transform HoldPoint => holdPoint != null ? holdPoint : transform;

    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
        if (interaction == null)
            interaction = GetComponent<PlayerInteraction>();
        if (health == null)
            health = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        if (health != null) health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (health != null) health.OnDeath -= HandleDeath;
    }

    // Ağır eşya elle alınamaz (el arabası gerekir), elde eşya varken yenisi alınamaz.
    public bool CanPickUp(ICarriable item)
        => item != null && !IsCarrying && item.Weight != CarryWeight.Heavy;

    public bool PickUp(ICarriable item)
    {
        if (!CanPickUp(item)) return false;

        carried = item;
        carried.OnPickedUp(gameObject);
        playerController.SetSpeedMultiplier(GameConstants.CARRY_SPEED_MULTIPLIER);
        return true;
    }

    public void Drop()
    {
        if (!IsCarrying) return;

        // Önümüzde etkileşim hedefi varsa oraya teslim et (craft ocağı kendi alanında devralır),
        // yoksa önümüze yere bırak.
        Component target = interaction != null ? interaction.Current as Component : null;
        Vector3 dropPos = target != null
            ? target.transform.position
            : transform.position + transform.forward * dropForwardDistance;

        ReleaseAt(dropPos);
    }

    // Ölüm gibi durumlarda: teslim hedefi aranmaz, eşya olduğumuz yere düşer.
    public void ForceDrop()
    {
        if (!IsCarrying) return;
        ReleaseAt(transform.position + transform.forward * dropForwardDistance);
    }

    // Tüm bırakma yolları buradan geçer ki hız çarpanı her durumda normale dönsün.
    private void ReleaseAt(Vector3 dropPos)
    {
        carried.OnDropped(dropPos);
        carried = null;
        playerController.SetSpeedMultiplier(1f);
    }

    // Oyuncu ölünce taşıdığı eşyayı bırakır — yoksa revive sonrası hız 0.6'da takılı kalıyordu.
    private void HandleDeath() => ForceDrop();
}
