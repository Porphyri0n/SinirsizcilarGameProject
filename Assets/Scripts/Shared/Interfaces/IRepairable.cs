using System;
using UnityEngine;

/// <summary>Tamir edilebilen yapılar (duvar)</summary>
public interface IRepairable
{
    float RepairCost { get; }
    ResourceType RepairResource { get; }
    bool NeedsRepair { get; }
    void Repair(float amount);
}
