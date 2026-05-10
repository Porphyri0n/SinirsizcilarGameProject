using System;
using UnityEngine;

// Haydut verisi — ticaret yolundaki ağaçlardan çıkan düşmanlar (Raider, Brute).
[CreateAssetMenu(fileName = "NewBandit", menuName = "Game/Bandit Data")]
public class BanditData : ScriptableObject
{
    public BanditType banditType;
    public string displayName;
    public float maxHealth;
    public float moveSpeed;
    public float attackDamage;
    public float attackInterval;
    public GameObject prefab;
}
