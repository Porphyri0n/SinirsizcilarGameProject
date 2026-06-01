using System;
using UnityEngine;

// Sahile varan geminin kaleye saldırı davranışı.
// ShipMovement.OnReachedShore'u dinler; sahile varınca attackInterval aralıklarla
// Önce duvarlara (Defense tag), duvarlar yıkılınca kaleye (Castle tag) top atışı yapar.
[RequireComponent(typeof(ShipMovement))]
public class ShipAttack : MonoBehaviour
{
    [SerializeField] private ShipData shipData;
    [SerializeField] private ShipMovement movement;

    [Header("Ateş Ayarları")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform muzzle;
    [SerializeField] private float projectileSpeed = 25f;

    [Header("Hedefleme Ayarları")]
    [Tooltip("Oyuncuya ateş etme ihtimali (0-1)")]
    [Range(0, 1)]
    [SerializeField] private float playerTargetChance = 0.3f;
    [Tooltip("Her atışta hedef değiştirme ihtimali")]
    [Range(0, 1)]
    [SerializeField] private float reTargetChance = 0.2f;

    private IDamageable currentTarget;
private bool isAttacking;
    private float nextAttackTime;

    private float AttackDamage => shipData != null ? shipData.attackDamage : 10f;
    private float AttackInterval => shipData != null ? Mathf.Max(0.1f, shipData.attackInterval) : 2f;

    private void Reset()
    {
        movement = GetComponent<ShipMovement>();
    }

    private void OnEnable()
    {
        // pool'dan tekrar kullanımda durumu sıfırla
        isAttacking = false;
        nextAttackTime = 0f;
        currentTarget = null;

        if (movement == null)
            movement = GetComponent<ShipMovement>();

        if (movement != null)
            movement.OnReachedShore += HandleReachedShore;
    }

    private void OnDisable()
    {
        if (movement != null)
            movement.OnReachedShore -= HandleReachedShore;
    }

    private void HandleReachedShore()
    {
        isAttacking = true;
        nextAttackTime = Time.time + AttackInterval;
        currentTarget = FindBestTarget();
    }

    private void Update()
    {
        if (!isAttacking) return;
        if (Time.time < nextAttackTime) return;

        // Hedef yıkıldıysa, kaybolduysa veya belirli bir ihtimalle rastgele başka hedef ara
        if (currentTarget == null || !currentTarget.IsAlive || UnityEngine.Random.value < reTargetChance)
            currentTarget = FindBestTarget();

        if (currentTarget == null) return;

        FireCannonball();
        nextAttackTime = Time.time + AttackInterval;
    }

    private void FireCannonball()
    {
        if (projectilePrefab == null) return;

        Transform origin = muzzle != null ? muzzle : transform;
        
        // Hedefe doğru yönü hesapla
        Vector3 targetPos = GetTargetPosition(currentTarget);
        
        // Trajectory hesapla
        Vector3 launchVelocity;
        float currentProjectileSpeed = projectileSpeed;
        
        // Eğer hedef çok uzaksa hızı mecburen biraz artırıyoruz ki ulaşabilsin
        if (!CalculateTrajectory(origin.position, targetPos, currentProjectileSpeed, out launchVelocity))
        {
            // Menzil dışıysa: minimum hızı hesapla (45 derece civarı en verimli açı için)
            float g = Mathf.Abs(Physics.gravity.y);
            Vector3 diff = targetPos - origin.position;
            Vector3 diffXZ = new Vector3(diff.x, 0, diff.z);
            float x = diffXZ.magnitude;
            float y = diff.y;
            
            // Minimum hız formülü: v = sqrt(g * (y + sqrt(x^2 + y^2)))
            float minSpeed = Mathf.Sqrt(g * (y + Mathf.Sqrt(x * x + y * y)));
            currentProjectileSpeed = minSpeed + 2f; // Biraz marj bırakalım
            
            CalculateTrajectory(origin.position, targetPos, currentProjectileSpeed, out launchVelocity);
        }

        // Mermiyi oluştur ve fırlat
        GameObject obj = Instantiate(projectilePrefab, origin.position, Quaternion.LookRotation(launchVelocity));
        Projectile projectile = obj.GetComponent<Projectile>();
        if (projectile != null)
        {
            // Projectile.Launch içinde rb.linearVelocity = direction.normalized * speed yapılıyor.
            // Bu yüzden direction ve hızı ayrı veriyoruz.
            projectile.Launch(launchVelocity.normalized, launchVelocity.magnitude, AttackDamage, 0f, true, gameObject);
        }
    }

    /// <summary>
    /// Verilen hızla hedefi vurmak için gereken fırlatma vektörünü hesaplar.
    /// </summary>
    private bool CalculateTrajectory(Vector3 start, Vector3 target, float speed, out Vector3 velocity)
    {
        velocity = Vector3.zero;
        Vector3 diff = target - start;
        Vector3 diffXZ = new Vector3(diff.x, 0, diff.z);
        float x = diffXZ.magnitude;
        float y = diff.y;
        float v = speed;
        float v2 = v * v;
        float g = Mathf.Abs(Physics.gravity.y);

        if (x < 0.01f)
        {
            velocity = Vector3.up * speed;
            return true;
        }

        float root = v2 * v2 - g * (g * (x * x) + 2 * y * v2);

        if (root < 0) return false; // Bu hızla bu mesafeye ulaşılamaz

        float sqrtRoot = Mathf.Sqrt(root);
        
        // Bombeli atış için +sqrtRoot (yüksek açı), direkt atış için -sqrtRoot (düşük açı) kullanılır.
        // Kullanıcı "bombeli" istediği için yüksek açıyı seçiyoruz.
        float tanTheta = (v2 + sqrtRoot) / (g * x);
        float theta = Mathf.Atan(tanTheta);

        velocity = diffXZ.normalized * Mathf.Cos(theta) * v;
        velocity.y = Mathf.Sin(theta) * v;

        return true;
    }

    private Vector3 GetTargetPosition(IDamageable target)
    {
        if (target is Component comp)
        {
            // Eğer bir Collider varsa merkezine nişan almayı dene
            Collider col = comp.GetComponentInChildren<Collider>();
            if (col != null) return col.bounds.center;
            
            return comp.transform.position;
        }
        return Vector3.zero;
    }

    private IDamageable FindBestTarget()
    {
        // 1. Rastgele şans ile oyuncuya mı yoksa surlara mı saldıracağına karar ver
        bool targetPlayer = UnityEngine.Random.value < playerTargetChance;

        if (targetPlayer)
        {
            IDamageable player = FindClosestPlayer();
            if (player != null) return player;
        }

        // 2. Oyuncu seçilmediyse veya bulunamadıysa surları ara
        IDamageable wall = FindClosestWall();
        if (wall != null) return wall;

        // 3. Surlar da yoksa (yıkılmışsa) ama yukarıda oyuncu aranmamışsa bir de oyuncuya bak
        if (!targetPlayer)
        {
            IDamageable playerFallback = FindClosestPlayer();
            if (playerFallback != null) return playerFallback;
        }

        // 4. En son çare kaleye saldır
        GameObject castleGo = GameObject.FindGameObjectWithTag(GameConstants.TAG_CASTLE);
        if (castleGo != null)
        {
            IDamageable castle = castleGo.GetComponent<IDamageable>();
            if (castle != null && castle.IsAlive) return castle;
        }

        return null;
    }

    private IDamageable FindClosestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag(GameConstants.TAG_PLAYER);
        IDamageable best = null;
        float minDistance = float.MaxValue;

        foreach (var go in players)
        {
            IDamageable dam = go.GetComponent<IDamageable>();
            if (dam != null && dam.IsAlive)
            {
                float d = Vector3.Distance(transform.position, go.transform.position);
                if (d < minDistance)
                {
                    minDistance = d;
                    best = dam;
                }
            }
        }
        return best;
    }

    private IDamageable FindClosestWall()
    {
        // Wall bileşeni olanları ara
        Wall[] walls = GameObject.FindObjectsByType<Wall>(FindObjectsSortMode.None);
        IDamageable bestWall = null;
        float minDistance = float.MaxValue;

        foreach (Wall wall in walls)
        {
            if (wall.IsAlive)
            {
                float dist = Vector3.Distance(transform.position, wall.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestWall = wall;
                }
            }
        }
        return bestWall;
    }

    // WaveSpawner / ObjectPooler yapılandırırken ShipData'yı buradan verir
    public void Configure(ShipData data)
    {
        shipData = data;
    }
}
