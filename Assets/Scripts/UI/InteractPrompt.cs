using System;
using UnityEngine;
using TMPro;

/// <summary>
/// Bir IInteractable nesnesinin üzerinde dünya-uzayında "[E] ..." metni gösterir (diegetic UI).
/// World space Canvas + kameraya dönen billboard. Yalnızca yerel oyuncu INTERACT_RANGE içindeyken
/// ve IInteractable.CanInteract true iken görünür; metin GetInteractPrompt()'tan gelir.
/// Prefab, etkileşilebilir nesnenin bir alt nesnesi olarak yerleştirilir.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class InteractPrompt : MonoBehaviour
{
    [SerializeField] private TMP_Text promptLabel;
    [SerializeField] private float verticalOffset = 1.5f;

    private Canvas canvas;
    private IInteractable interactable;
    private Transform anchor;            // Prompt'un üzerinde durduğu nesne
    private Camera viewCamera;
    private GameObject localPlayer;      // Mesafe ölçümü ve CanInteract için

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        interactable = GetComponentInParent<IInteractable>();
        anchor = transform.parent != null ? transform.parent : transform;
        Hide();
    }

    /// <summary>Yerel oyuncuyu ve bakış kamerasını bağlar (DiegeticUIManager veya spawn kurulumu çağırır).</summary>
    public void Bind(GameObject player, Camera camera)
    {
        localPlayer = player;
        viewCamera = camera;
    }

    private void LateUpdate()
    {
        if (viewCamera == null)
            viewCamera = Camera.main;
        if (localPlayer == null)
            localPlayer = GameObject.FindGameObjectWithTag(GameConstants.TAG_PLAYER);

        if (interactable == null || localPlayer == null || viewCamera == null)
        {
            Hide();
            return;
        }

        transform.position = anchor.position + Vector3.up * verticalOffset;

        float distance = Vector3.Distance(localPlayer.transform.position, anchor.position);
        bool visible = distance <= GameConstants.INTERACT_RANGE && interactable.CanInteract(localPlayer);
        if (!visible)
        {
            Hide();
            return;
        }

        if (promptLabel != null)
            promptLabel.text = interactable.GetInteractPrompt();
        canvas.enabled = true;

        // Billboard — yüzü kameraya dönük
        transform.rotation = Quaternion.LookRotation(transform.position - viewCamera.transform.position);
    }

    private void Hide()
    {
        if (canvas != null)
            canvas.enabled = false;
    }
}
