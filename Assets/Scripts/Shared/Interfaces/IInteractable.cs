using System;
using UnityEngine;

/// <summary>E tuşuyla etkileşim</summary>
public interface IInteractable
{
    string GetInteractPrompt();
    bool CanInteract(GameObject player);
    void Interact(GameObject player);
}
