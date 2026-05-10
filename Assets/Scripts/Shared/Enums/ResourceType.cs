using System;
using UnityEngine;

/// <summary>Kaynak türleri — tamamı ticari kervanlardan gelir</summary>
public enum ResourceType
{
    // Temel kaynaklar — erken wave'lerdeki kervanlar getirir
    Wood,
    Stone,
    Iron,

    // Gelişmiş kaynaklar — ileri wave'lerdeki kervanlar getirir
    Steel,
    Gold,
    Crystal
}
