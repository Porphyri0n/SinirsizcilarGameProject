using System;
using UnityEngine;

// Kulede oyuncunun mouse ile nişan alması.
// Kameradan mouse pozisyonuna raycast atar, hedef noktayı bulur,
// pivot'tan hedefe doğru yön hesaplar ve TowerController.Operate(dir) çağırır.
// Otomatik targeting YOK — yalnızca oyuncunun gösterdiği yön.
[RequireComponent(typeof(TowerController))]
public class TowerAiming : MonoBehaviour
{
    [SerializeField] private TowerController tower;
    [SerializeField] private Camera towerCamera;        // TowerController ile aynı kamera
    [SerializeField] private Transform aimPivot;        // Yön referansı (TowerController ile aynı pivot)
    [SerializeField] private LayerMask aimMask = ~0;
    [SerializeField] private float maxAimDistance = 50f;

    [Header("Menzil Göstergesi (opsiyonel)")]
    [SerializeField] private LineRenderer rangeLine;    // Hedef noktaya çizgi
    [SerializeField] private bool showRange = true;
    [SerializeField] private Color validColor = Color.green;    // menzilde, canlı hedef → geçerli atış
    [SerializeField] private Color invalidColor = Color.red;    // menzil dışı veya hedef yok

    private void Awake()
    {
        if (tower == null) tower = GetComponent<TowerController>();
    }

    private void OnDisable()
    {
        if (rangeLine != null) rangeLine.positionCount = 0;
    }

    private void Update()
    {
        if (tower == null || !tower.IsOccupied) return;
        if (towerCamera == null || aimPivot == null) return;

        Vector3 target = GetAimPoint(out IDamageable aimed);
        Vector3 dir = target - aimPivot.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        tower.Operate(dir);
        UpdateRangeLine(target, IsValidShot(target, aimed));
    }

    // Mouse → kamera ray → ilk çarpılan yüzey; ıskalarsa maxAimDistance kadar ileri nokta.
    // aimed: çarpılan nesnedeki IDamageable (varsa) — atış geçerliliği için.
    private Vector3 GetAimPoint(out IDamageable aimed)
    {
        aimed = null;
        Ray ray = towerCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimMask))
        {
            aimed = hit.collider.GetComponentInParent<IDamageable>();
            return hit.point;
        }
        return ray.origin + ray.direction * maxAimDistance;
    }

    // Geçerli atış: hedef kule menzili içinde ve canlı bir IDamageable'a nişanlı.
    private bool IsValidShot(Vector3 target, IDamageable aimed)
    {
        if (Vector3.Distance(aimPivot.position, target) > tower.Range) return false;
        return aimed != null && aimed.IsAlive;
    }

    private void UpdateRangeLine(Vector3 target, bool valid)
    {
        if (!showRange || rangeLine == null) return;
        rangeLine.positionCount = 2;
        rangeLine.SetPosition(0, aimPivot.position);
        rangeLine.SetPosition(1, target);

        Color c = valid ? validColor : invalidColor;
        rangeLine.startColor = c;
        rangeLine.endColor = c;
    }
}
