using System;
using System.Collections;
using UnityEngine;
using TMPro;

// Kervan bildirimi — OnCaravanApproaching gelince "TICARI KERVAN YAKLASIYOR" yazisi
// fade in/out gosterir ve kervanin yoldaki konumunu duman/bayrak isaretiyle takip eder.
// Kervan kaleye varinca veya yok edilince isaret kapanir.
public class CaravanUI : MonoBehaviour
{
    [Header("Duyuru Yazisi")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private float fadeDuration = 0.6f;
    [SerializeField] private float holdDuration = 2.5f;
    [SerializeField] private string approachText = "TICARI KERVAN YAKLASIYOR";

    [Header("Yol Isareti (duman/bayrak)")]
    [SerializeField] private Transform marker;              // Kervanin ustunde duran duman/bayrak
    [SerializeField] private Vector3 markerOffset = new Vector3(0f, 3f, 0f);

    private Transform trackedCaravan;
    private Coroutine announceRoutine;

    private void Awake()
    {
        if (group != null) group.alpha = 0f;
        if (marker != null) marker.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        EventBus.OnCaravanApproaching += HandleApproaching;
        EventBus.OnCaravanArrived += HandleArrived;
        EventBus.OnCaravanDestroyed += HandleDestroyed;
    }

    private void OnDisable()
    {
        EventBus.OnCaravanApproaching -= HandleApproaching;
        EventBus.OnCaravanArrived -= HandleArrived;
        EventBus.OnCaravanDestroyed -= HandleDestroyed;
    }

    private void HandleApproaching(CaravanData data)
    {
        Announce();
        BeginTracking();
    }

    private void HandleArrived(CaravanData data) => StopTracking();
    private void HandleDestroyed() => StopTracking();

    private void Announce()
    {
        if (label != null) label.text = approachText;
        if (announceRoutine != null) StopCoroutine(announceRoutine);
        announceRoutine = StartCoroutine(AnnounceRoutine());
    }

    private void BeginTracking()
    {
        // Kervan event'le birlikte sahnede oldugundan tag ile bulunur
        GameObject caravan = GameObject.FindGameObjectWithTag(GameConstants.TAG_CARAVAN);
        trackedCaravan = caravan != null ? caravan.transform : null;
        if (marker != null) marker.gameObject.SetActive(trackedCaravan != null);
    }

    private void StopTracking()
    {
        trackedCaravan = null;
        if (marker != null) marker.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (trackedCaravan == null || marker == null) return;
        marker.position = trackedCaravan.position + markerOffset;
    }

    private IEnumerator AnnounceRoutine()
    {
        yield return Fade(0f, 1f);
        yield return new WaitForSeconds(holdDuration);
        yield return Fade(1f, 0f);
        announceRoutine = null;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (group == null) yield break;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        group.alpha = to;
    }
}
