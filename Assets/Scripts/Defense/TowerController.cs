using System;
using UnityEngine;
using Photon.Pun;

// Oyuncu kontrollü kule taban sınıfı (IOperable + IInteractable).
// "[E] Kuleye Gir" ile girilir, kamera kule bakış açısına geçer, oyuncu nişan alır.
// E veya Escape ile çıkılır. CannonTower / ArcherTower bu sınıftan türer.
public class TowerController : MonoBehaviour, IOperable, IInteractable
{
    [Header("Veri")]
    [SerializeField] protected DefenseData data;

    [Header("Referanslar")]
    [SerializeField] private Transform aimPivot;        // Nişana göre dönen kısım (namlu/yay)
    [SerializeField] private Camera towerCamera;        // Kuledeyken aktif olan kamera
    [SerializeField] private Transform exitPoint;       // Çıkınca oyuncunun konumlanacağı nokta

    [Header("Ateş")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform muzzle;          // Mermi çıkış noktası (aimPivot'un altında döner)
    [SerializeField] private float projectileSpeed = 25f;

    [Header("Etkileşim")]
    [SerializeField] private string enterPrompt = "[E] Kuleye Gir";

    private GameObject operatorPlayer;
    private int operatorPlayerID = -1;
    private int enterFrame = -1;
    private float lastFireTime = -999f;

    public bool IsOccupied => operatorPlayer != null;
    public int OperatorPlayerID => operatorPlayerID;

    protected GameObject Operator => operatorPlayer;
    protected DefenseType DefenseType => data != null ? data.defenseType : DefenseType.CannonTower;

    private float FireInterval => (data != null && data.fireRate > 0f) ? 1f / data.fireRate : 1f;
    private float Damage => data != null ? data.damage : GameConstants.SWORD_BASE_DAMAGE;
    private float SplashRadius => data != null ? data.splashRadius : 0f;
    private float Range => data != null ? data.range : 30f;

    // Cannon parabolik (true), Archer düz (false). Alt sınıf belirler.
    protected virtual bool ProjectileUsesGravity => false;

    // ── IInteractable ────────────────────────────────────────────────────
    public string GetInteractPrompt() => enterPrompt;
    public bool CanInteract(GameObject player) => !IsOccupied;

    public void Interact(GameObject player)
    {
        if (!IsOccupied)
            Enter(player);
    }

    // ── IOperable ────────────────────────────────────────────────────────
    public void Enter(GameObject player)
    {
        if (IsOccupied || player == null) return;

        operatorPlayer = player;
        operatorPlayerID = ResolvePlayerID(player);
        enterFrame = Time.frameCount;
        SetPlayerControlEnabled(player, false);

        if (towerCamera != null) towerCamera.enabled = true;

        EventBus.FireTowerEntered(operatorPlayerID, DefenseType);
        OnEntered();
    }

    public void Exit(GameObject player)
    {
        if (!IsOccupied) return;

        int pid = operatorPlayerID;
        GameObject leaving = operatorPlayer;

        if (towerCamera != null) towerCamera.enabled = false;
        if (exitPoint != null && leaving != null)
            leaving.transform.position = exitPoint.position;
        SetPlayerControlEnabled(leaving, true);

        operatorPlayer = null;
        operatorPlayerID = -1;

        EventBus.FireTowerExited(pid, DefenseType);
        OnExited();
    }

    public void Operate(Vector3 aimDirection)
    {
        if (!IsOccupied || aimPivot == null) return;
        if (aimDirection.sqrMagnitude < 0.0001f) return;
        aimPivot.rotation = Quaternion.LookRotation(aimDirection.normalized);
    }

    protected virtual void Update()
    {
        if (!IsOccupied || Time.frameCount == enterFrame) return;

        if (Input.GetKeyDown(GameConstants.INTERACT_KEY) || Input.GetKeyDown(KeyCode.Escape))
        {
            Exit(operatorPlayer);
            return;
        }

        if (Input.GetMouseButtonDown(0) && Time.time >= lastFireTime + FireInterval)
            Fire();
    }

    // Sol tık ateşi — muzzle yönüne mermi spawn'lar, fireRate kadar cooldown, FireTowerFired duyurur.
    private void Fire()
    {
        if (projectilePrefab == null) return;

        lastFireTime = Time.time;

        Transform origin = muzzle != null ? muzzle : (aimPivot != null ? aimPivot : transform);
        Vector3 dir = origin.forward;

        GameObject obj = Instantiate(projectilePrefab, origin.position, Quaternion.LookRotation(dir));
        Projectile projectile = obj.GetComponent<Projectile>();
        if (projectile != null)
            projectile.Launch(dir, projectileSpeed, Damage, SplashRadius, ProjectileUsesGravity, gameObject);

        EventBus.FireTowerFired(DefenseType, origin.position + dir * Range);
    }

    // Alt sınıflar ateş/menzil kurulumu için override eder.
    protected virtual void OnEntered() { }
    protected virtual void OnExited() { }

    private static int ResolvePlayerID(GameObject player)
    {
        PhotonView view = player.GetComponent<PhotonView>();
        return view != null ? view.OwnerActorNr : player.GetInstanceID();
    }

    private static void SetPlayerControlEnabled(GameObject player, bool enabled)
    {
        if (player == null) return;
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null) controller.enabled = enabled;
    }
}
