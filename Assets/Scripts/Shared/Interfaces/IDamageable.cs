using System;
using UnityEngine;

/// <summary>Hasar alabilen her şey</summary>
public interface IDamageable
{
    float CurrentHealth { get; }
    float MaxHealth { get; }
    bool IsAlive { get; }
    void TakeDamage(float amount, Vector3 hitPoint);
    event Action OnDeath;
}
