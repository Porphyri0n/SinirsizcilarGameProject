using System;
using UnityEngine;

/// <summary>Ticari kervan durumu — at arabası ile gelir</summary>
public enum CaravanState
{
    Approaching,    // Ticaret yolundan yaklaşıyor
    UnderAttack,    // Haydut saldırısında — oyuncular korumalı
    Arrived,        // Kaleye ulaştı, kaynakları teslim ediyor
    Departing       // Geri dönüyor
}
