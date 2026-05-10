using System;
using UnityEngine;

// Gemi verisi — Light, Medium, Heavy, Boss için ayrı SO asset'leri oluşturulur.
[CreateAssetMenu(fileName = "NewShip", menuName = "Game/Ship Data")]
public class ShipData : ScriptableObject
{
    public ShipType shipType;
    public string displayName;
    public float maxHealth;
    public float moveSpeed;
    public float attackDamage;
    public float attackInterval;
    public GameObject prefab;
}
