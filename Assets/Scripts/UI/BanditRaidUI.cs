using System;
using System.Collections;
using UnityEngine;
using TMPro;

// Haydut saldirisi uyarisi — OnBanditRaid gelince "HAYDUT SALDIRISI!" yazisi yanip soner
// ve saldiri yonunu gosteren bir ekran oku (screen indicator) belirir.
// Ok, kameradan saldiri konumuna dogru ekran kenarinda doner. Sure bitince kapanir.
public class BanditRaidUI : MonoBehaviour
{
    [Header("Uyari Yazisi")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private string raidText = "HAYDUT SALDIRISI!";
    [SerializeField] private float blinkInterval = 0.3f;

    [Header("Yon Gostergesi")]
    [SerializeField] private Camera viewCamera;             // bos ise Camera.main
    [SerializeField] private RectTransform directionArrow;  // anchor: ekran merkezi
    [SerializeField] private float indicatorRadius = 220f;

    [Header("Sure")]
    [SerializeField] private float showDuration = 4f;

    private Vector3 raidPosition;
    private bool active;
    private Coroutine routine;

    private void Awake()
    {
        if (group != null) group.alpha = 0f;
        if (directionArrow != null) directionArrow.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        EventBus.OnBanditRaid += HandleBanditRaid;
    }

    private void OnDisable()
    {
        EventBus.OnBanditRaid -= HandleBanditRaid;
    }

    private void HandleBanditRaid(int count, Vector3 position)
    {
        raidPosition = position;
        if (label != null) label.text = raidText;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(RaidRoutine());
    }

    private IEnumerator RaidRoutine()
    {
        active = true;
        if (directionArrow != null) directionArrow.gameObject.SetActive(true);

        float elapsed = 0f;
        bool on = false;
        float nextBlink = 0f;

        while (elapsed < showDuration)
        {
            elapsed += Time.deltaTime;

            // Yanip sonen yazi
            if (Time.time >= nextBlink)
            {
                on = !on;
                nextBlink = Time.time + blinkInterval;
                if (group != null) group.alpha = on ? 1f : 0.2f;
            }
            yield return null;
        }

        active = false;
        if (group != null) group.alpha = 0f;
        if (directionArrow != null) directionArrow.gameObject.SetActive(false);
        routine = null;
    }

    private void Update()
    {
        if (!active) return;
        UpdateIndicator();
    }

    private void UpdateIndicator()
    {
        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        if (cam == null || directionArrow == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(raidPosition);
        Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;

        // Kamera arkasindaysa ekran yonunu ters cevir
        if (screenPos.z < 0f)
        {
            screenPos.x = Screen.width - screenPos.x;
            screenPos.y = Screen.height - screenPos.y;
        }

        Vector2 dir = (Vector2)screenPos - center;
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        directionArrow.anchoredPosition = dir * indicatorRadius;
        directionArrow.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);    // ok yukari bakacak sekilde
    }
}
