using System;
using UnityEngine;

/// <summary>
/// Kaynak verisi
/// Oluşturan: Evren | Kullanan: Koray (taşıma), Ziya (UI)
/// Temel: Wood, Stone, Iron — Gelişmiş: Steel, Gold, Crystal. Tüm kaynaklar ticari kervanlardan gelir.
/// </summary>
[CreateAssetMenu(fileName = "NewResource", menuName = "Game/Resource Data")]
public class ResourceData : ScriptableObject
{
    public ResourceType resourceType;
    public ResourceTier tier;
    public string displayName;
    public Sprite icon;
    public CarryWeight weight;
    public GameObject worldPrefab;          // Yerde duran kaynak modeli
}
