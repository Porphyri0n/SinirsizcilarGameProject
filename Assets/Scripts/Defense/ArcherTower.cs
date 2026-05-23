using System;
using UnityEngine;

// Okçu kulesi — orta menzil, düşük hasar, hızlı ateş. TowerController'dan türer.
// Düz uçan ok atar (yerçekimsiz); tek hedef (DefenseData.splashRadius = 0).
// Ateş, cooldown ve mermi spawn mantığı tabanda (TowerController); değerler DefenseData'dan.
public class ArcherTower : TowerController
{
    protected override bool ProjectileUsesGravity => false;     // düz, hızlı ok
}
