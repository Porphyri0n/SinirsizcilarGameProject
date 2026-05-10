using System;
using UnityEngine;

// Wave verisi — sonsuz wave sistemi için tek bir dalganın tanımı.
[CreateAssetMenu(fileName = "NewWave", menuName = "Game/Wave Data")]
public class WaveData : ScriptableObject
{
    public int waveNumber;
    public float prepPhaseDuration;
    public WaveShipEntry[] ships;
    public float spawnInterval;
    public bool isBossWave;
}
