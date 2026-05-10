using System;
using UnityEngine;

// İksir verisi — Strength (hasar) ve Hearing (ses menzili).
[CreateAssetMenu(fileName = "NewPotion", menuName = "Game/Potion Data")]
public class PotionData : ScriptableObject
{
    public PotionType potionType;
    public string displayName;
    public float duration;
    public float effectValue;
    public Sprite icon;
    public GameObject worldPrefab;
    public Color screenTintColor;
}
