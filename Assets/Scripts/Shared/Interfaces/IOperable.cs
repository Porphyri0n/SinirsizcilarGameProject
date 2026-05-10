using System;
using UnityEngine;

/// <summary>Oyuncunun girip kontrol edebildiği kuleler</summary>
public interface IOperable
{
    bool IsOccupied { get; }
    int OperatorPlayerID { get; }
    void Enter(GameObject player);
    void Exit(GameObject player);
    void Operate(Vector3 aimDirection);
}
