using System;
using UnityEngine;

// Önümüzdeki en yakın IInteractable'ı bulur, E'ye basınca Interact çağırır.
public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private LayerMask interactMask = ~0;

    private IInteractable current;
    public IInteractable Current => current;

    private void Awake()
    {
        if (aimOrigin == null) aimOrigin = transform;
    }

    private void Update()
    {
        current = FindInteractable();

        if (current != null && Input.GetKeyDown(GameConstants.INTERACT_KEY))
            current.Interact(gameObject);
    }

    private IInteractable FindInteractable()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, GameConstants.INTERACT_RANGE, interactMask, QueryTriggerInteraction.Collide);
        Vector3 forward = aimOrigin.forward;

        IInteractable best = null;
        float bestSqr = float.MaxValue;

        foreach (Collider c in hits)
        {
            IInteractable interactable = c.GetComponentInParent<IInteractable>();
            if (interactable == null || !interactable.CanInteract(gameObject)) continue;

            Vector3 to = c.transform.position - transform.position;
            if (Vector3.Dot(forward, to.normalized) < 0.3f) continue; // kabaca önümüzde

            float sqr = to.sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = interactable; }
        }
        return best;
    }
}
