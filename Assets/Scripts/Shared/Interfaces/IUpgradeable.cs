using System;
using UnityEngine;

/// <summary>Yükseltilebilen yapılar (kule, ocak, el arabası, silah)</summary>
public interface IUpgradeable
{
    UpgradeLevel CurrentLevel { get; }
    UpgradeData GetNextUpgrade();
    bool CanUpgrade();
    void Upgrade();
    event Action<UpgradeLevel> OnUpgraded;
}
