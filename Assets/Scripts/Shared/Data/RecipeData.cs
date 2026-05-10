using System;
using UnityEngine;

// Craft tarifi — malzemeler + çıktı (silah ya da savunma) + gerekli ocak seviyesi.
[CreateAssetMenu(fileName = "NewRecipe", menuName = "Game/Recipe Data")]
public class RecipeData : ScriptableObject
{
    public string recipeName;
    public RecipeIngredient[] ingredients;
    public WeaponType? outputWeapon;
    public DefenseType? outputDefense;
    public UpgradeLevel requiredStationLevel;
    public float craftDuration;
    public Sprite recipeIcon;
}
