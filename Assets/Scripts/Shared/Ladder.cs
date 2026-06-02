using UnityEngine;

/// <summary>
/// Merdiven objesi. Oyuncu içine girdiğinde SetAtLadder ile PlayerController'ı bilgilendirir.
/// Hem otomatik (W'ya basarak) hem de manuel (E ile etkileşim) tırmanmayı destekler.
/// </summary>
public class Ladder : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactPrompt = "Tırman (E)";

    public string GetInteractPrompt() => interactPrompt;

    public bool CanInteract(GameObject player)
    {
        if (player.TryGetComponent<PlayerController>(out var controller))
        {
            return !controller.IsClimbing;
        }
        return false;
    }

    public void Interact(GameObject player)
    {
        if (player.TryGetComponent<PlayerController>(out var controller))
        {
            controller.StartClimbing();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Root'ta PlayerController olabilir (Network yapısı gereği)
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            player.SetAtLadder(true, this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            player.SetAtLadder(false, null);
        }
    }
}
