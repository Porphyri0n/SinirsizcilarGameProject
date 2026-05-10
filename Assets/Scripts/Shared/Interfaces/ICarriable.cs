using System;
using UnityEngine;

/// <summary>Taşınabilir eşyalar</summary>
public interface ICarriable
{
    CarryWeight Weight { get; }
    string ItemName { get; }
    void OnPickedUp(GameObject carrier);
    void OnDropped(Vector3 dropPosition);
}
