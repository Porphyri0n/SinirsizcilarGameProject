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

    [Header("Etkileşim")]
    [SerializeField] private string enterPrompt = "[E] Kuleye Gir";

    private GameObject operatorPlayer;
    private int operatorPlayerID = -1;
    private int enterFrame = -1;

    public bool IsOccupied => operatorPlayer != null;
    public int OperatorPlayerID => operatorPlayerID;

    protected GameObject Operator => operatorPlayer;
    protected DefenseType DefenseType => data != null ? data.defenseType : DefenseType.CannonTower;

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
            Exit(operatorPlayer);
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
