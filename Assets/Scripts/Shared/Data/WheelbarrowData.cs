using System;
using UnityEngine;

// El arabası verisi — seviyeye göre kapasite ve hız.
[CreateAssetMenu(fileName = "NewWheelbarrow", menuName = "Game/Wheelbarrow Data")]
public class WheelbarrowData : ScriptableObject
{
    public UpgradeLevel level;
    public int capacity;
    public float speedMultiplier;
    public string displayName;
}
