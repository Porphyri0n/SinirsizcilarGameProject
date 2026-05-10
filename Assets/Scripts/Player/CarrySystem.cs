using System;
using UnityEngine;

// Oyuncunun tek eşya taşıma sistemi (ICarriable). Taşırken hız CARRY_SPEED_MULTIPLIER ile yavaşlar.
[RequireComponent(typeof(PlayerController))]
public class CarrySystem : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Transform holdPoint;       // Eşya taşınırken buraya tutturulur
    [SerializeField] private float dropForwardDistance = 1f;

    private ICarriable carried;

    public bool IsCarrying => carried != null;
    public ICarriable Carried => carried;
    public Transform HoldPoint => holdPoint != null ? holdPoint : transform;

    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
    }

    public bool CanPickUp(ICarriable item) => item != null && !IsCarrying;

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

        Vector3 dropPos = transform.position + transform.forward * dropForwardDistance;
        carried.OnDropped(dropPos);
        carried = null;
        playerController.SetSpeedMultiplier(1f);
    }
}
