using System;
using UnityEngine;
using Unity.Netcode;

// Önümüzdeki en yakın IInteractable'ı bulur, E'ye basınca Interact çağırır.
public class PlayerInteraction : NetworkBehaviour
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
        if (!IsOwner) return;

        current = FindInteractable();

        // UI açıkken veya mouse serbestken etkileşimi engelle (örn. menü kapatırken E'ye basınca geri açılmasın)
        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (current != null && Input.GetKeyDown(GameConstants.INTERACT_KEY))
            current.Interact(gameObject);
    }

    private IInteractable FindInteractable()
    {
        // Mesafe kontrolünü oyuncu merkezinden yapıyoruz (3rd person kameradan değil)
        Vector3 origin = transform.position + Vector3.up * 1f; // Yaklaşık göğüs hizası
        Collider[] hits = Physics.OverlapSphere(origin, GameConstants.INTERACT_RANGE, interactMask, QueryTriggerInteraction.Collide);
        
        // Bakış yönünü kameradan alıyoruz
        Vector3 lookOrigin = aimOrigin != null ? aimOrigin.position : origin;
        Vector3 forward = aimOrigin != null ? aimOrigin.forward : transform.forward;

        IInteractable best = null;
        float bestSqr = float.MaxValue;

        foreach (Collider c in hits)
        {
            // Kendi collider'ımızı görmeyelim
            if (c.transform.root == transform.root) continue;

            IInteractable interactable = c.GetComponentInParent<IInteractable>();
            if (interactable == null || !interactable.CanInteract(gameObject)) continue;

            Vector3 targetPoint = c.bounds.center;
            Vector3 to = targetPoint - lookOrigin; // Kameraya göre yön
            
            float distSqr = (targetPoint - origin).sqrMagnitude; // Oyuncuya göre mesafe
            
            // Bakış yönü kontrolü
            if (Vector3.Dot(forward, to.normalized) < 0.2f) continue;

            if (distSqr < bestSqr) { bestSqr = distSqr; best = interactable; }
        }
        return best;
    }
}
