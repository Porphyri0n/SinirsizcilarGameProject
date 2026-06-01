using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using Unity.Netcode;

public class RespawnController : MonoBehaviour
{
    private VisualElement root;
    private Label countdownLabel;
    private float respawnTime = 15f;
    private Coroutine respawnCoroutine;
    private int localPlayerId = -1;

    private void OnEnable()
    {
        var uiDoc = GetComponent<UIDocument>();
        if (uiDoc == null) return;

        root = uiDoc.rootVisualElement;
        countdownLabel = root.Q<Label>("countdownLabel");
        
        // Gizle başlangıçta
        root.style.display = DisplayStyle.None;

        EventBus.OnPlayerDied += HandlePlayerDied;
        EventBus.OnPlayerRevived += HandlePlayerRevived;
    }

    private void OnDisable()
    {
        EventBus.OnPlayerDied -= HandlePlayerDied;
        EventBus.OnPlayerRevived -= HandlePlayerRevived;
    }

    private void HandlePlayerDied(int pid, Vector3 pos)
    {
        Debug.Log($"[RespawnController] Player Died Event Received: pid={pid}, LocalClientId={NetworkManager.Singleton.LocalClientId}");
        
        // Kendi karakterimiz mi öldü?
        if (NetworkManager.Singleton != null && (ulong)pid == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("[RespawnController] Local player died. Starting respawn sequence.");
            localPlayerId = pid;
            if (respawnCoroutine != null) StopCoroutine(respawnCoroutine);
            respawnCoroutine = StartCoroutine(RespawnSequence(pid));
        }
    }

    private void HandlePlayerRevived(int pid)
    {
        Debug.Log($"[RespawnController] Player Revived Event Received: pid={pid}");
        // Eğer zaten diriltildiysek (örn. Sela ile), sayacı durdur
        if (pid == localPlayerId && respawnCoroutine != null)
        {
            Debug.Log("[RespawnController] Local player revived early. Stopping countdown.");
            StopCoroutine(respawnCoroutine);
            respawnCoroutine = null;
            root.style.display = DisplayStyle.None;
        }
    }

    private IEnumerator RespawnSequence(int pid)
    {
        root.style.display = DisplayStyle.Flex;
        float remaining = respawnTime;

        while (remaining > 0)
        {
            if (countdownLabel != null)
                countdownLabel.text = $"YENİDEN DOĞUŞA: {Mathf.CeilToInt(remaining)}s";
            
            remaining -= Time.deltaTime;
            yield return null;
        }

        root.style.display = DisplayStyle.None;
        EventBus.FirePlayerRevived(pid);
        respawnCoroutine = null;
    }
}
