using System;
using UnityEngine;

// Savunma/kule verisi — CannonTower, ArcherTower vb. için SO.
[CreateAssetMenu(fileName = "NewDefense", menuName = "Game/Defense Data")]
public class DefenseData : ScriptableObject
{
    public DefenseType defenseType;
    public string displayName;
    public float maxHealth;
    public float damage;
    public float range;
    public float fireRate;
    public float splashRadius;
    public GameObject prefab;
}
