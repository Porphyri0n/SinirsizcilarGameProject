using System;
using UnityEngine;
using Unity.Netcode;

// Oyuncu kontrollü kule taban sınıfı (IOperable + IInteractable).
// "[E] Kuleye Gir" ile girilir, kamera kule bakış açısına geçer, oyuncu nişan alır.
// E veya Escape ile çıkılır. CannonTower / ArcherTower bu sınıftan türer.
public class TowerController : NetworkBehaviour, IOperable, IInteractable
{
    [Header("Veri")]
    [SerializeField] protected DefenseData data;

    [Header("Referanslar")]
    [SerializeField] private Transform aimPivot;        // Nişana göre dönen kısım (namlu/yay)
    [SerializeField] private TowerCameraRig cameraRig;  // Giriş/çıkış kamera geçişi (yumuşak)
    [SerializeField] private Transform exitPoint;       // Çıkınca oyuncunun konumlanacağı nokta

    [Header("Ateş")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform muzzle;          // Mermi çıkış noktası (aimPivot'un altında döner)
    [SerializeField] private float projectileSpeed = 25f;

    [Header("Etkileşim")]
    [SerializeField] private string enterPrompt = "[E] Kuleye Gir";

    [Header("Görsel & Ses")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private float fireShakeMagnitude = 0.25f;
    [SerializeField] private float fireShakeDuration = 0.15f;

    private GameObject operatorPlayer;
    private int operatorPlayerID = -1;
    private int enterFrame = -1;
    private float lastFireTime = -999f;

    public Transform Muzzle => muzzle;
    public bool IsOccupied => operatorPlayer != null;
    public int OperatorPlayerID => operatorPlayerID;
    public GameObject OperatorPlayer => operatorPlayer;

    protected GameObject Operator => operatorPlayer;
    protected DefenseType DefenseType => data != null ? data.defenseType : DefenseType.CannonTower;

    private float FireInterval => (data != null && data.fireRate > 0f) ? 1f / data.fireRate : 1f;
    private float Damage => data != null ? data.damage : GameConstants.SWORD_BASE_DAMAGE;
    private float SplashRadius => data != null ? data.splashRadius : 0f;
    public float Range => data != null ? data.range : 30f;

    // Cannon parabolik (true), Archer düz (false). Alt sınıf belirler.
    protected virtual bool ProjectileUsesGravity => false;

    protected virtual void Awake()
    {
        if (cameraRig == null) cameraRig = GetComponent<TowerCameraRig>();
    }

    // ── IInteractable ────────────────────────────────────────────────────
    public string GetInteractPrompt() => enterPrompt;
    public bool CanInteract(GameObject player) => !IsOccupied;

    private int exitFrame = -1;

    public void Interact(GameObject player)
    {
        if (!IsOccupied && Time.frameCount != exitFrame)
            Enter(player);
    }

    public void Enter(GameObject player)
    {
        if (IsOccupied || player == null) return;

        SetOccupant(player, ResolvePlayerID(player));
        enterFrame = Time.frameCount;
        
        // Operatör kuledeyken fizik çakışmalarını ve titremeyi önlemek için CharacterController'ı TAMAMEN kapat
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        SetPlayerControlEnabled(player, false);

        if (cameraRig != null) cameraRig.EnterView();

        EventBus.FireTowerEntered(operatorPlayerID, DefenseType);
        OnEntered();
    }

    // Network senkronu için yan etkisiz (kamera/input değiştirmeyen) doluluk güncellemesi
    public void SetOccupant(GameObject player, int pid)
    {
        operatorPlayer = player;
        operatorPlayerID = pid;
    }

    public void Exit(GameObject player)
    {
        if (!IsOccupied) return;

        int pid = operatorPlayerID;
        GameObject leaving = operatorPlayer;

        if (cameraRig != null && IsLocalOperator()) cameraRig.ExitView();
        
        // Teleport öncesi kapalı olduğundan emin ol (Enter'da kapatmıştık)
        var cc = leaving.GetComponent<CharacterController>();
        if (cc != null && IsLocalActor(pid)) cc.enabled = false;

        if (exitPoint != null && leaving != null && IsLocalActor(pid))
        {
            // ExitPoint biraz daha içerde ve güvenli bir yükseklikte olmalı
            leaving.transform.position = exitPoint.position;
        }
        
        // Teleport bittikten sonra fiziği geri aç
        if (cc != null && IsLocalActor(pid)) cc.enabled = true;
        
        if (IsLocalActor(pid)) SetPlayerControlEnabled(leaving, true);

        operatorPlayer = null;
        operatorPlayerID = -1;
        exitFrame = Time.frameCount;

        EventBus.FireTowerExited(pid, DefenseType);
        OnExited();
    }

    private static bool IsLocalActor(int pid)
    {
        return Unity.Netcode.NetworkManager.Singleton != null 
            && (ulong)pid == Unity.Netcode.NetworkManager.Singleton.LocalClientId;
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

        if (!IsLocalOperator()) return;

        // E veya Escape ile kuleyi terk et
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
        {
            Exit(operatorPlayer);
            return;
        }

        if (Input.GetMouseButtonDown(0) && Time.time >= lastFireTime + FireInterval)
            Fire();
    }

    private void OnEnable()
    {
        EventBus.OnTowerFired += HandleTowerFiredGlobal;
    }

    private void OnDisable()
    {
        EventBus.OnTowerFired -= HandleTowerFiredGlobal;
    }

    // Sol tık ateşi — muzzle yönüne mermi spawn'lar, fireRate kadar cooldown, FireTowerFired duyurur.
    private void Fire()
    {
        if (projectilePrefab == null) return;

        lastFireTime = Time.time;

        // Muzzle yoksa aimPivot, o da yoksa kule merkezini kullan
        Transform origin = muzzle != null ? muzzle : (aimPivot != null ? aimPivot : transform);
        
        // Kamera bakış yönünü veya aimPivot'un forward'ını baz alalım (muzzle bazen yanlış durabiliyor)
        Vector3 dir = aimPivot != null ? aimPivot.forward : origin.forward;
        
        // Lokal olarak hemen oynat
        PlayFireEffects(origin.position, dir);

        // Server'a bildir
        FireServerRpc(origin.position, dir);

        EventBus.FireTowerFired(DefenseType, origin.position + dir * Range);
    }

    [ServerRpc(RequireOwnership = false)]
    private void FireServerRpc(Vector3 originPos, Vector3 direction)
    {
        GameObject obj = Instantiate(projectilePrefab, originPos, Quaternion.LookRotation(direction));
        Projectile projectile = obj.GetComponent<Projectile>();
        
        if (projectile != null)
        {
            Projectile.ProjectileSyncData syncData = new Projectile.ProjectileSyncData
            {
                direction = direction,
                speed = projectileSpeed,
                damage = Damage,
                splashRadius = SplashRadius,
                useGravity = ProjectileUsesGravity,
                team = ProjectileTeam.Player,
                owner = new NetworkObjectReference(gameObject),
                operatorToIgnore = new NetworkObjectReference(operatorPlayer)
            };

            projectile.SetSyncData(syncData);
            
            NetworkObject netObj = obj.GetComponent<NetworkObject>();
            if (netObj != null)
                netObj.Spawn();
                
            // Server'da mermiyi başlat (Data zaten set edildi, ClientRpc'den önce spawn ve launch önemli)
            projectile.Launch(direction, projectileSpeed, Damage, SplashRadius, ProjectileUsesGravity, ProjectileTeam.Player, gameObject, operatorPlayer);
        }

        // Diğer client'lara görsel efektleri yayınla
        PlayFireEffectsClientRpc(originPos, direction);
    }

    [ClientRpc]
    private void PlayFireEffectsClientRpc(Vector3 pos, Vector3 dir)
    {
        // Operatör zaten lokal olarak oynattı, tekrar oynatmasın
        if (IsLocalOperator()) return;
        
        PlayFireEffects(pos, dir);
    }

    private void HandleTowerFiredGlobal(DefenseType type, Vector3 targetPos)
    {
        // Bizim kule tipimiz değilse veya zaten operatör bizsek (zaten oynattık) atla.
        if (type != DefenseType || IsLocalOperator()) return;

        Transform origin = muzzle != null ? muzzle : (aimPivot != null ? aimPivot : transform);
        Vector3 dir = (targetPos - origin.position).normalized;
        
        // PlayFireEffects(origin.position, dir); // Artık ClientRpc ile yapılıyor
    }

    public bool IsLocalOperator()
    {
        return IsOccupied && Unity.Netcode.NetworkManager.Singleton != null 
            && (ulong)operatorPlayerID == Unity.Netcode.NetworkManager.Singleton.LocalClientId;
    }

    private void PlayFireEffects(Vector3 pos, Vector3 dir)
    {
        Transform origin = muzzle != null ? muzzle : (aimPivot != null ? aimPivot : transform);

        // Feedback: VFX
        if (muzzleFlashPrefab != null)
        {
            Instantiate(muzzleFlashPrefab, pos, Quaternion.LookRotation(dir), origin);
        }

        // Feedback: SFX
        if (fireSound != null)
        {
            AudioSource.PlayClipAtPoint(fireSound, pos);
        }

        // Feedback: Shake (Sadece kuleyi kullanan oyuncu sarsılsın)
        if (CameraShake.Instance != null && IsLocalOperator())
        {
            CameraShake.Instance.Shake(fireShakeDuration, fireShakeMagnitude);
        }
    }

    // Alt sınıflar ateş/menzil kurulumu için override eder.
    protected virtual void OnEntered() { }
    protected virtual void OnExited() { }

    private static int ResolvePlayerID(GameObject player)
    {
        NetworkObject view = player.GetComponent<NetworkObject>();
        return view != null ? (int)view.OwnerClientId : player.GetInstanceID();
    }

    private static void SetPlayerControlEnabled(GameObject player, bool enabled)
    {
        if (player == null) return;
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null) controller.enabled = enabled;
    }
}
