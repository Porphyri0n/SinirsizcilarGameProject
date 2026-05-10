using System;
using UnityEngine;

// Silah verisi — kılıç ve kalkan (ok/yay yok).
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Game/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public WeaponType weaponType;
    public UpgradeLevel tier;
    public string displayName;
    public float damage;            // Kılıç hasarı
    public float blockAmount;       // Kalkan hasar azaltma (0-1)
    public float attackSpeed;
    public Sprite icon;
    public GameObject prefab;
}
