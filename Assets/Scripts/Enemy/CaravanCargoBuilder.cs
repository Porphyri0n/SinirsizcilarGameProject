using System;
using System.Collections.Generic;
using UnityEngine;

// Kervan gelişmiş kaynak sistemi — kargoyu wave'e göre üretir.
// Temel kaynaklar (Wood/Stone/Iron) her kervanda bulunur; gelişmiş kaynaklar (Steel/Gold/Crystal)
// yalnızca wave >= minWaveForAdvanced olduğunda eklenir. Miktarlar wave numarasıyla orantılı artar.
public static class CaravanCargoBuilder
{
    private static readonly ResourceType[] Basic = { ResourceType.Wood, ResourceType.Stone, ResourceType.Iron };
    private static readonly ResourceType[] Advanced = { ResourceType.Steel, ResourceType.Gold, ResourceType.Crystal };

    private const int BASIC_BASE_AMOUNT = 2;        // temel kaynak taban miktarı
    private const int ADVANCED_BASE_AMOUNT = 1;     // gelişmiş kaynak taban miktarı (kilit açıldığında)

    // Wave'e göre kervan kargosu üret.
    public static CaravanCargoEntry[] Build(int wave, int minWaveForAdvanced)
    {
        wave = Mathf.Max(1, wave);
        List<CaravanCargoEntry> cargo = new List<CaravanCargoEntry>();

        int basicAmount = BASIC_BASE_AMOUNT + wave;             // wave ilerledikçe artar
        foreach (ResourceType t in Basic)
            cargo.Add(Entry(t, basicAmount));

        if (wave >= minWaveForAdvanced)
        {
            int advancedAmount = ADVANCED_BASE_AMOUNT + Mathf.Max(0, wave - minWaveForAdvanced);
            foreach (ResourceType t in Advanced)
                cargo.Add(Entry(t, advancedAmount));
        }

        return cargo.ToArray();
    }

    private static CaravanCargoEntry Entry(ResourceType type, int amount)
    {
        return new CaravanCargoEntry { resourceType = type, amount = amount };
    }
}
