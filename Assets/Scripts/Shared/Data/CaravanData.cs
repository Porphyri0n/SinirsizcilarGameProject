using System;
using UnityEngine;

// Ticari kervan verisi — at arabası ile doğu/batıdan kaynak getirir.
[CreateAssetMenu(fileName = "NewCaravan", menuName = "Game/Caravan Data")]
public class CaravanData : ScriptableObject
{
    public string displayName;          // "Tüccar Kervanı"
    public float maxHealth;             // Haydut saldırısında kervanın canı
    public float moveSpeed;
    public CaravanCargoEntry[] cargo;   // Getirdiği kaynaklar
    public float banditChance;          // Haydut saldırısı olasılığı (0-1)
    public int minWaveForAdvanced;      // Gelişmiş kaynak getirmeye başlama wave'i
    public GameObject prefab;
}
