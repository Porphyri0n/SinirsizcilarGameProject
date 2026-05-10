using System;
using UnityEngine;

// Kervan kaleye ulaşınca kargosundaki kaynakları depoya (ResourceStorage) ekler.
// Her kaynak için EventBus.FireResourceReceived çağrılır. Gelişmiş kaynaklar (Steel/Gold/Crystal)
// yalnızca CaravanData.minWaveForAdvanced'e ulaşılmış wave'lerde teslim alınır.
public class CaravanReceiver : MonoBehaviour
{
    [SerializeField] private ResourceStorage storage;

    private int currentWave;

    private void Awake()
    {
        if (storage == null)
            storage = FindObjectOfType<ResourceStorage>();
    }

    private void OnEnable()
    {
        EventBus.OnCaravanArrived += HandleCaravanArrived;
        EventBus.OnWaveStart += HandleWaveStart;
    }

    private void OnDisable()
    {
        EventBus.OnCaravanArrived -= HandleCaravanArrived;
        EventBus.OnWaveStart -= HandleWaveStart;
    }

    private void HandleWaveStart(int waveNumber) => currentWave = waveNumber;

    private void HandleCaravanArrived(CaravanData data)
    {
        if (data == null || data.cargo == null) return;

        foreach (CaravanCargoEntry entry in data.cargo)
        {
            if (entry == null || entry.amount <= 0) continue;
            if (IsAdvanced(entry.resourceType) && currentWave < data.minWaveForAdvanced) continue;

            if (storage != null)
                storage.Deposit(entry.resourceType, entry.amount);
            EventBus.FireResourceReceived(entry.resourceType, entry.amount);
        }
    }

    // Steel, Gold, Crystal = gelişmiş (Advanced) kaynaklar.
    private static bool IsAdvanced(ResourceType type)
    {
        return type == ResourceType.Steel || type == ResourceType.Gold || type == ResourceType.Crystal;
    }
}
