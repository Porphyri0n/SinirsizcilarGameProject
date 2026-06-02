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

    [Header("Ayarlar")]
    [SerializeField] private float sensitivity = 2f;
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 45f;

    private float currentYaw;
    private float currentPitch;
    private bool wasOccupied;
    private float occupiedDuration;
    private bool hasInitializedRotation;

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
        if (tower == null || !tower.IsOccupied)
        {
            wasOccupied = false;
            occupiedDuration = 0f;
            hasInitializedRotation = false;
            return;
        }

        if (!tower.IsLocalOperator()) return;

        if (!wasOccupied)
        {
            // Giriş yapıldığında mevcut rotasyonu yakala
            Vector3 euler = aimPivot.eulerAngles;
            currentYaw = euler.y;
            currentPitch = euler.x;
            if (currentPitch > 180f) currentPitch -= 360f;
            wasOccupied = true;
            occupiedDuration = (towerCamera == null) ? 0.4f : 0f;
            hasInitializedRotation = false;
        }

        occupiedDuration += Time.deltaTime;

        // Mouse Look: Cursor'ı kilitle
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        currentYaw += mouseX;
        currentPitch -= mouseY;
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

        // Kule geçişi tamamlandıktan sonra (0.4s) kamerayı döndür ve nişan almaya izin ver
        if (occupiedDuration >= 0.4f)
        {
            if (towerCamera != null)
            {
                if (!hasInitializedRotation)
                {
                    Vector3 camEuler = towerCamera.transform.eulerAngles;
                    currentYaw = camEuler.y;
                    currentPitch = camEuler.x;
                    if (currentPitch > 180f) currentPitch -= 360f;
                    hasInitializedRotation = true;
                }

                towerCamera.transform.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            }

            // Calculate direction based on camera hit point
            Vector3 targetPoint = GetAimPoint(out IDamageable aimed);
            Transform fireOrigin = (tower.Muzzle != null) ? tower.Muzzle : aimPivot;
            Vector3 aimDir = (targetPoint - fireOrigin.position).normalized;
            
            tower.Operate(aimDir);

            // Menzil göstergesi için hedef nokta tespiti
            UpdateRangeLine(targetPoint, IsValidShot(targetPoint, aimed));
        }
        else
        {
            if (rangeLine != null) rangeLine.positionCount = 0;
        }
    }

    // Kamera veya Pivot ileri yönüne raycast
    private Vector3 GetAimPoint(out IDamageable aimed)
    {
        aimed = null;
        
        Ray ray;
        if (towerCamera != null)
        {
            // Viewport'un tam ortası (0.5, 0.5) crosshair noktasıdır
            ray = towerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        }
        else
        {
            ray = new Ray(aimPivot.position, aimPivot.forward);
        }
        
        RaycastHit[] hits = Physics.RaycastAll(ray, maxAimDistance, aimMask);
        if (hits.Length > 0)
        {
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                // Kuleyi veya kuleyi kullanan oyuncuyu vurursak yoksay (kendi kendine nişan alıp dönmeyi önler)
                if (hit.transform.IsChildOf(transform))
                    continue;
                if (tower != null && tower.OperatorPlayer != null && hit.transform.IsChildOf(tower.OperatorPlayer.transform))
                    continue;

                aimed = hit.collider.GetComponentInParent<IDamageable>();
                return hit.point;
            }
        }
        return ray.origin + ray.direction * maxAimDistance;
    }

    // Geçerli atış: hedef kule menzili içinde ve canlı bir IDamageable'a nişanlı.
    private bool IsValidShot(Vector3 target, IDamageable aimed)
    {
        if (Vector3.Distance(aimPivot.position, target) > tower.Range) return false;
        return aimed != null && (aimed.IsAlive);
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
