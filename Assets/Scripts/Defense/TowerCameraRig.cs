using System;
using System.Collections;
using UnityEngine;

// Kule giriş/çıkış kamera sistemi — oyuncu kamerası ile kule kamerası arasında yumuşak geçiş.
// Girişte: kule kamerası oyuncunun o anki bakışından başlar, kule bakış pozuna (home) lerp'ler.
// Çıkışta: tersine oyuncu bakışına geri lerp'ler, sonra oyuncu kamerasını devreye alır.
// Kuleler sabit olduğu için kule bakış pozu (home) Awake'te dünya pozu olarak yakalanır.
// TowerController EnterView/ExitView çağırır; kamera enable/disable ve geçiş burada yönetilir.
public class TowerCameraRig : MonoBehaviour
{
    [SerializeField] private Camera towerCamera;            // Kuledeyken aktif kamera (geçişte taşınır)
    [SerializeField] private float transitionDuration = 0.4f;

    private Camera playerCamera;                            // Girişte yakalanan oyuncu kamerası
    private Vector3 homeWorldPos;                           // Kuledeki oturmuş bakış pozu (dünya)
    private Quaternion homeWorldRot;
    private Coroutine routine;

    private void Awake()
    {
        if (towerCamera == null) return;

        homeWorldPos = towerCamera.transform.position;
        homeWorldRot = towerCamera.transform.rotation;
        towerCamera.enabled = false;
    }

    // Oyuncu kuleye girdi — oyuncu bakışından kule bakışına yumuşak geçiş.
    public void EnterView()
    {
        if (towerCamera == null) return;

        playerCamera = Camera.main;

        // Kule kamerasını oyuncunun mevcut pozundan başlat, sonra home'a lerp et
        if (playerCamera != null)
            towerCamera.transform.SetPositionAndRotation(playerCamera.transform.position, playerCamera.transform.rotation);

        towerCamera.enabled = true;
        if (playerCamera != null) playerCamera.enabled = false;

        StartTransition(homeWorldPos, homeWorldRot, false);
    }

    // Oyuncu kuleden çıktı — kule bakışından oyuncu bakışına geri dön, sonra oyuncu kamerasını aç.
    public void ExitView()
    {
        if (towerCamera == null) return;

        if (playerCamera != null)
            StartTransition(playerCamera.transform.position, playerCamera.transform.rotation, true);
        else
            FinishExit();
    }

    private void StartTransition(Vector3 targetPos, Quaternion targetRot, bool restorePlayerAtEnd)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Transition(targetPos, targetRot, restorePlayerAtEnd));
    }

    private IEnumerator Transition(Vector3 targetPos, Quaternion targetRot, bool restorePlayerAtEnd)
    {
        Vector3 startPos = towerCamera.transform.position;
        Quaternion startRot = towerCamera.transform.rotation;

        float t = 0f;
        while (transitionDuration > 0f && t < transitionDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / transitionDuration);
            towerCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(startPos, targetPos, k),
                Quaternion.Slerp(startRot, targetRot, k));
            yield return null;
        }

        towerCamera.transform.SetPositionAndRotation(targetPos, targetRot);
        routine = null;

        if (restorePlayerAtEnd) FinishExit();
    }

    private void FinishExit()
    {
        towerCamera.enabled = false;
        if (playerCamera != null) playerCamera.enabled = true;

        // Sonraki giriş için kamerayı kule bakış pozuna geri koy
        towerCamera.transform.SetPositionAndRotation(homeWorldPos, homeWorldRot);
    }
}
