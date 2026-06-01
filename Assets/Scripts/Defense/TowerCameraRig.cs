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
    private Vector3 homeLocalPos;                           // Kuledeki oturmuş bakış pozu (local)
    private Quaternion homeLocalRot;
    private Coroutine routine;

    private void Awake()
    {
        if (towerCamera == null) return;

        homeLocalPos = towerCamera.transform.localPosition;
        homeLocalRot = towerCamera.transform.localRotation;
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

        StartTransition(homeLocalPos, homeLocalRot, false);
    }

    // Oyuncu kuleden çıktı — kule bakışından oyuncu bakışına geri dön, sonra oyuncu kamerasını aç.
    public void ExitView()
    {
        if (towerCamera == null) return;

        if (playerCamera != null)
        {
            // Oyuncu kamerasının dünya pozuna dönmeliyiz
            StartTransition(playerCamera.transform.position, playerCamera.transform.rotation, true, true);
        }
        else
            FinishExit();
    }

    private void StartTransition(Vector3 target, Quaternion rot, bool restorePlayerAtEnd, bool isWorld = false)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Transition(target, rot, restorePlayerAtEnd, isWorld));
    }

    private IEnumerator Transition(Vector3 target, Quaternion targetRot, bool restorePlayerAtEnd, bool isWorld)
    {
        Vector3 startPos = isWorld ? towerCamera.transform.position : towerCamera.transform.localPosition;
        Quaternion startRot = isWorld ? towerCamera.transform.rotation : towerCamera.transform.localRotation;

        float t = 0f;
        while (transitionDuration > 0f && t < transitionDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / transitionDuration);
            
            if (isWorld)
            {
                towerCamera.transform.SetPositionAndRotation(
                    Vector3.Lerp(startPos, target, k),
                    Quaternion.Slerp(startRot, targetRot, k));
            }
            else
            {
                towerCamera.transform.localPosition = Vector3.Lerp(startPos, target, k);
                towerCamera.transform.localRotation = Quaternion.Slerp(startRot, targetRot, k);
            }
            yield return null;
        }

        if (isWorld)
            towerCamera.transform.SetPositionAndRotation(target, targetRot);
        else
        {
            towerCamera.transform.localPosition = target;
            towerCamera.transform.localRotation = targetRot;
        }

        routine = null;

        if (restorePlayerAtEnd) FinishExit();
    }

    private void FinishExit()
    {
        towerCamera.enabled = false;
        if (playerCamera != null) playerCamera.enabled = true;

        // Sonraki giriş için kamerayı kule bakış pozuna geri koy
        towerCamera.transform.localPosition = homeLocalPos;
        towerCamera.transform.localRotation = homeLocalRot;
    }
}
