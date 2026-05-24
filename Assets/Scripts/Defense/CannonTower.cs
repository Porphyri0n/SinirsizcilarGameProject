using System;
using UnityEngine;

// Top kulesi — uzun menzil, ağır hasar, yavaş ateş. TowerController'dan türer.
// Parabolik gülle atar (yerçekimli); alan hasarı DefenseData.splashRadius ile uygulanır.
// Ateş, cooldown ve mermi spawn mantığı tabanda (TowerController); değerler DefenseData'dan.
public class CannonTower : TowerController
{
    protected override bool ProjectileUsesGravity => true;      // parabolik gülle
}
