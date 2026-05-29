using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

// Sela mekaniği: ölen oyuncuyu diriltme.
// OnPlayerDied -> ceset kaydı. Cesede yaklasan oyuncu "[E] Sela Oku" gorur.
// Okuma kuleden yapilir; sure boyunca okuyucu kulede kalmalidir.
// Sure bitince OnPlayerRevived(deadPid) yayinlanir.
public class SelaSystem : MonoBehaviour
{
    public static SelaSystem Instance { get; private set; }

    [SerializeField] private float selaDuration = 8f;

    // dead player id -> ceset pozisyon anchor'u
    private readonly Dictionary<int, Transform> corpses = new Dictionary<int, Transform>();
    private readonly HashSet<int> playersInTower = new HashSet<int>();
    private readonly HashSet<int> activeReaders = new HashSet<int>();
    // dead player id -> 0..1 okuma ilerlemesi (gosterge bunu okur)
    private readonly Dictionary<int, float> selaProgress = new Dictionary<int, float>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        EventBus.OnPlayerDied += HandlePlayerDied;
        EventBus.OnTowerEntered += HandleTowerEntered;
        EventBus.OnTowerExited += HandleTowerExited;
    }

    private void OnDisable()
    {
        EventBus.OnPlayerDied -= HandlePlayerDied;
        EventBus.OnTowerEntered -= HandleTowerEntered;
        EventBus.OnTowerExited -= HandleTowerExited;
    }

    // ── KAYIT ────────────────────────────────────────────────────────────
    // Ceset prefab'i (ragdoll) hazirsa Transform'u verilir; aksi halde
    // OnPlayerDied callback'i pozisyondan bir anchor uretir.
    public void RegisterCorpse(int deadPlayerId, Transform corpseTransform)
        => corpses[deadPlayerId] = corpseTransform;

    public void UnregisterCorpse(int deadPlayerId) => corpses.Remove(deadPlayerId);

    public bool HasCorpse(int deadPlayerId) => corpses.ContainsKey(deadPlayerId);
    public bool IsReaderInTower(int readerPlayerId) => playersInTower.Contains(readerPlayerId);
    public bool IsReading(int readerPlayerId) => activeReaders.Contains(readerPlayerId);

    // Cesedin dirilis ilerlemesi (0..1). Aktif okuma yoksa false doner.
    public bool TryGetSelaProgress(int deadPlayerId, out float normalized)
        => selaProgress.TryGetValue(deadPlayerId, out normalized);

    public Vector3 GetCorpsePosition(int deadPlayerId)
    {
        return corpses.TryGetValue(deadPlayerId, out Transform t) && t != null
            ? t.position
            : Vector3.zero;
    }

    // ── EVENT HANDLERS ───────────────────────────────────────────────────
    private void HandlePlayerDied(int pid, Vector3 pos)
    {
        // Ragdoll henuz kayitli degilse pozisyonda bos anchor yarat
        if (corpses.ContainsKey(pid)) return;
        GameObject anchor = new GameObject("CorpseAnchor_" + pid);
        anchor.transform.position = pos;
        corpses[pid] = anchor.transform;
    }

    private void HandleTowerEntered(int pid, DefenseType t) => playersInTower.Add(pid);
    private void HandleTowerExited(int pid, DefenseType t) => playersInTower.Remove(pid);

    // ── SELA ─────────────────────────────────────────────────────────────
    // Cesetle etkilesimden gelen istek. Okuyucu kulede degilse veya
    // ceset yoksa basarisiz olur.
    public bool BeginSela(int readerPid, int deadPid)
    {
        if (!corpses.ContainsKey(deadPid)) return false;
        if (!playersInTower.Contains(readerPid)) return false;
        if (activeReaders.Contains(readerPid)) return false;

        activeReaders.Add(readerPid);
        EventBus.FireSelaStarted(readerPid, deadPid);
        StartCoroutine(SelaCoroutine(readerPid, deadPid));
        return true;
    }

    private IEnumerator SelaCoroutine(int readerPid, int deadPid)
    {
        float t = 0f;
        selaProgress[deadPid] = 0f;
        while (t < selaDuration)
        {
            // Okuyucu kuleden cikarsa veya ceset duserse okuma iptal
            if (!playersInTower.Contains(readerPid) || !corpses.ContainsKey(deadPid))
            {
                activeReaders.Remove(readerPid);
                selaProgress.Remove(deadPid);
                yield break;
            }
            t += Time.deltaTime;
            selaProgress[deadPid] = Mathf.Clamp01(t / selaDuration);
            yield return null;
        }

        activeReaders.Remove(readerPid);
        selaProgress.Remove(deadPid);
        corpses.Remove(deadPid);
        EventBus.FirePlayerRevived(deadPid);
    }
}

// Cesedin uzerine eklenen IInteractable. "[E] Sela Oku" prompt'unu gosterir
// ve okuma istegini SelaSystem'e iletir.
public class SelaCorpseInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private int deadPlayerId;
    [SerializeField] private string prompt = "[E] Sela Oku";

    public void SetDeadPlayerId(int pid)
    {
        deadPlayerId = pid;
        if (SelaSystem.Instance != null)
            SelaSystem.Instance.RegisterCorpse(pid, transform);
    }

    public string GetInteractPrompt() => prompt;

    public bool CanInteract(GameObject player)
    {
        SelaSystem sys = SelaSystem.Instance;
        if (sys == null) return false;
        int pid = ResolvePlayerID(player);
        return sys.HasCorpse(deadPlayerId) && sys.IsReaderInTower(pid) && !sys.IsReading(pid);
    }

    public void Interact(GameObject player)
    {
        SelaSystem sys = SelaSystem.Instance;
        if (sys == null) return;
        sys.BeginSela(ResolvePlayerID(player), deadPlayerId);
    }

    private static int ResolvePlayerID(GameObject player)
    {
        NetworkObject view = player.GetComponent<NetworkObject>();
        return view != null ? (int)view.OwnerClientId : player.GetInstanceID();
    }
}

// Sela okunurken cesedin uzerinde beliren diegetic ilerleme cubugu (world space, billboard).
// OnSelaStarted ile acilir, SelaSystem'den ilerlemeyi okuyup fill'i olcekler; revive ya da
// okuma iptalinde gizlenir. fill: X ekseninde 0..1 olceklenen dolum nesnesi.
[RequireComponent(typeof(Canvas))]
public class SelaProgressIndicator : MonoBehaviour
{
    [SerializeField] private Transform fill;
    [SerializeField] private float verticalOffset = 2.2f;

    private Canvas canvas;
    private Camera viewCamera;
    private int deadPid = -1;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        Hide();
    }

    private void OnEnable()
    {
        EventBus.OnSelaStarted += HandleSelaStarted;
        EventBus.OnPlayerRevived += HandleRevived;
    }

    private void OnDisable()
    {
        EventBus.OnSelaStarted -= HandleSelaStarted;
        EventBus.OnPlayerRevived -= HandleRevived;
    }

    private void HandleSelaStarted(int readerPid, int dead)
    {
        deadPid = dead;
        if (canvas != null) canvas.enabled = true;
    }

    private void HandleRevived(int pid)
    {
        if (pid == deadPid) Hide();
    }

    private void LateUpdate()
    {
        if (deadPid < 0) return;
        if (viewCamera == null) viewCamera = Camera.main;

        SelaSystem sys = SelaSystem.Instance;
        // Sistem yoksa veya okuma iptal olduysa (ilerleme kaydi yok) gizle
        if (sys == null || viewCamera == null || !sys.TryGetSelaProgress(deadPid, out float p))
        {
            Hide();
            return;
        }

        if (fill != null)
            fill.localScale = new Vector3(Mathf.Clamp01(p), 1f, 1f);

        transform.position = sys.GetCorpsePosition(deadPid) + Vector3.up * verticalOffset;
        // Billboard — yuzu kameraya donuk
        transform.rotation = Quaternion.LookRotation(transform.position - viewCamera.transform.position);
    }

    private void Hide()
    {
        deadPid = -1;
        if (canvas != null) canvas.enabled = false;
    }
}
