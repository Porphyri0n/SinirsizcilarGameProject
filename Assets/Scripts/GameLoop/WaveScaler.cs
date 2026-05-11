using System;
using UnityEngine;

// Sonsuz wave zorluk hesabı. Wave numarasından prosedürel olarak gemi kompozisyonu ve scaling üretir.
// Zorluk WAVE_DIFFICULTY_MULTIPLIER üssüyle büyür; her BOSS_WAVE_INTERVAL'da bir boss wave gelir.
public static class WaveScaler
{
    // Wave N'in zorluk çarpanı (gemi can/hasar scaling'i için de kullanılır)
    public static float DifficultyMultiplier(int waveNumber)
    {
        return Mathf.Pow(GameConstants.WAVE_DIFFICULTY_MULTIPLIER, Mathf.Max(0, waveNumber - 1));
    }

    public static bool IsBossWave(int waveNumber)
    {
        return waveNumber > 0 && waveNumber % GameConstants.BOSS_WAVE_INTERVAL == 0;
    }

    // Wave N için tam gemi planı — sayılar wave ilerledikçe artar, ağır gemiler ileri wave'lerde devreye girer
    public static WavePlan Plan(int waveNumber)
    {
        waveNumber = Mathf.Max(1, waveNumber);
        float scale = DifficultyMultiplier(waveNumber);

        WavePlan plan = new WavePlan
        {
            waveNumber = waveNumber,
            isBossWave = IsBossWave(waveNumber),
            lightShips = Mathf.Max(1, Mathf.RoundToInt(3f * scale)),
            mediumShips = waveNumber >= 3 ? Mathf.RoundToInt(1.5f * scale) : 0,
            heavyShips = waveNumber >= 6 ? Mathf.RoundToInt(0.6f * scale) : 0,
            spawnInterval = Mathf.Max(0.4f, 2f - 0.05f * (waveNumber - 1))
        };

        if (plan.isBossWave)
        {
            // Boss zaten zorluk kattığı için normal gemi sayısı biraz düşer; ileri boss wave'lerde birden çok boss
            plan.bossShips = 1 + waveNumber / (GameConstants.BOSS_WAVE_INTERVAL * 3);
            plan.lightShips = Mathf.RoundToInt(plan.lightShips * 0.6f);
            plan.mediumShips = Mathf.RoundToInt(plan.mediumShips * 0.6f);
            plan.heavyShips = Mathf.RoundToInt(plan.heavyShips * 0.6f);
        }

        return plan;
    }

    // Bir ShipType'tan wave N'de kaç tane çıkacağı
    public static int CountOf(ShipType type, int waveNumber)
    {
        WavePlan plan = Plan(waveNumber);
        switch (type)
        {
            case ShipType.Light: return plan.lightShips;
            case ShipType.Medium: return plan.mediumShips;
            case ShipType.Heavy: return plan.heavyShips;
            case ShipType.Boss: return plan.bossShips;
            default: return 0;
        }
    }
}

// Bir wave'in prosedürel içeriği — WaveManager bunu okuyup gemileri spawn eder
public struct WavePlan
{
    public int waveNumber;
    public bool isBossWave;
    public int lightShips;
    public int mediumShips;
    public int heavyShips;
    public int bossShips;
    public float spawnInterval;

    public int TotalShips => lightShips + mediumShips + heavyShips + bossShips;
}
