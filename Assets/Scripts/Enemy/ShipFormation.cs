using System;
using UnityEngine;

// Dalgayla gelen gemileri yan yana / sıralı diziye yerleştirir.
// WaveSpawner her gemi için index verir, buradan offset'i alır ve spawn pozisyonuna ekler.
// Kuzeyden geldikleri için lateral = X ekseni, depth = Z ekseni (geriye doğru sıralar).
public class ShipFormation : MonoBehaviour
{
    [Header("Formasyon Parametreleri")]
    [SerializeField] private int shipsPerRow = 4;           // bir sıradaki gemi sayısı
    [SerializeField] private float lateralSpacing = 5f;     // yanyana gemi arası mesafe
    [SerializeField] private float depthSpacing = 6f;       // arka sıralarla ön sıra arası
    [SerializeField] private bool centerRows = true;        // sırayı X=0 etrafında ortala

    // index'inci geminin spawn pozisyonuna eklenecek offset.
    public Vector3 GetOffset(int index)
    {
        if (shipsPerRow <= 0) shipsPerRow = 1;

        int row = index / shipsPerRow;
        int col = index % shipsPerRow;

        float lateral = centerRows
            ? (col - (shipsPerRow - 1) * 0.5f) * lateralSpacing
            : col * lateralSpacing;

        return new Vector3(lateral, 0f, row * depthSpacing);
    }

    // baz pozisyonun üstüne formasyon offsetini uygular.
    public Vector3 GetFormationPosition(Vector3 basePos, int index)
    {
        return basePos + GetOffset(index);
    }
}
