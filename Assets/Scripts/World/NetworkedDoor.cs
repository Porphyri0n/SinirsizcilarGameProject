using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Networked door that opens and closes using Unity Netcode for GameObjects.
/// Rotates around a hinge transform.
/// </summary>
public class NetworkedDoor : NetworkBehaviour, IInteractable
{
    [Header("Settings")]
    [SerializeField] private Transform hingeTransform;
    [SerializeField] private float rotationSpeed = 5f;

    private NetworkVariable<bool> isOpen = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    public string GetInteractPrompt()
    {
        return isOpen.Value ? "Kapat (E)" : "Aç (E)";
    }

    public bool CanInteract(GameObject player)
    {
        return true;
    }

    public void Interact(GameObject player)
    {
        ToggleDoorRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ToggleDoorRpc()
    {
        isOpen.Value = !isOpen.Value;
    }

    private void Update()
    {
        if (hingeTransform == null) return;

        float targetY = isOpen.Value ? 90f : 0f;
        Quaternion targetRotation = Quaternion.Euler(0, targetY, 0);
        
        // Use Slerp for smooth rotation
        hingeTransform.localRotation = Quaternion.Slerp(
            hingeTransform.localRotation, 
            targetRotation, 
            Time.deltaTime * rotationSpeed
        );
    }
}
