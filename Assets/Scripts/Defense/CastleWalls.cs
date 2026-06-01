using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tüm kale surlarının can yönetimini merkezi olarak yapar.
/// </summary>
public class CastleWalls : MonoBehaviour
{
    public static CastleWalls Instance { get; private set; }

    private List<Wall> walls = new List<Wall>();
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        RefreshWallList();
    }

    public void RefreshWallList()
    {
        walls.Clear();
        Wall[] foundWalls = FindObjectsByType<Wall>(FindObjectsSortMode.None);
        walls.AddRange(foundWalls);
        
        // Duvarları isimlerine göre sırala (CastleWall1, CastleWall2...)
        // Böylece UI'da hangi barın hangi duvara ait olduğu sabit kalır.
        walls.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        
        UpdateUI();
    }

    /// <summary>
    /// Bir duvara hasar verir. Wall.TakeDamage tarafından merkezi yönetim için çağrılır.
    /// </summary>
    public void TakeDamage(Wall wall, float amount, Vector3 hitPoint)
    {
        if (wall == null || amount <= 0f) return;

        // Gerçek azaltma işlemini duvarda yap
        wall.ApplyDamageInternal(amount, hitPoint);
        
        // Münferit duvar UI güncellemesini tetikle
        int index = walls.IndexOf(wall);
        if (index != -1)
        {
            EventBus.FireWallHealthChanged(index, wall.CurrentHealth, wall.MaxHealth);
        }

        // Toplam UI güncellemesini de merkezi olarak tetikle
        UpdateUI();
    }

    public void UpdateUI()
    {
        float totalMax = 0f;
        float totalCurrent = 0f;

        for (int i = 0; i < walls.Count; i++)
        {
            var wall = walls[i];
            if (wall != null)
            {
                totalMax += wall.MaxHealth;
                totalCurrent += wall.CurrentHealth;
                
                // İlk kurulum veya toplu güncelleme için her duvarın kendi event'ini de atalım
                EventBus.FireWallHealthChanged(i, wall.CurrentHealth, wall.MaxHealth);
            }
        }

        if (totalMax > 0f)
        {
            EventBus.FireWallsDamaged(totalCurrent, totalMax);
        }
    }

    public void RegisterWall(Wall wall)
    {
        if (!walls.Contains(wall))
        {
            walls.Add(wall);
            walls.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
            UpdateUI();
        }
    }
}
