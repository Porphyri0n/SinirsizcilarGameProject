using System;
using UnityEngine;

// Yükseltme verisi — ocak, kule, el arabası, silah yükseltmeleri.
[CreateAssetMenu(fileName = "NewUpgrade", menuName = "Game/Upgrade Data")]
public class UpgradeData : ScriptableObject
{
    public string upgradeName;
    public UpgradeLevel fromLevel;
    public UpgradeLevel toLevel;
    public RecipeIngredient[] cost;
    public float upgradeTime;
    public string description;
}
