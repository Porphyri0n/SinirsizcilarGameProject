using System;
using UnityEngine;

// Düşman gemisinin hareketi — kuzeyden (denizden) sahile doğru ilerler.
// Sahile varınca durur; saldırıyı ShipAttack devralsın diye OnReachedShore tetiklenir.
public class ShipMovement : MonoBehaviour
{
    [SerializeField] private ShipData shipData;
    [SerializeField] private Transform shoreTarget;     // Sahildeki iniş noktası — WaveSpawner bağlar
    [SerializeField] private float arrivalDistance = 1.5f;

    private Vector3 formationOffset;                    // her gemiye farklı slot — sahilde üst üste binmesin

    public bool HasArrived { get; private set; }
    public event Action OnReachedShore;

    private float MoveSpeed => shipData != null ? shipData.moveSpeed : 3f;

    private void OnEnable()
    {
        HasArrived = false;             // pool'dan tekrar kullanımda sıfırla
        formationOffset = Vector3.zero;
    }

    private float currentSpeed = 0f;
    [SerializeField] private float acceleration = 0.5f;

    private void Update()
    {
        if (HasArrived) return;

        if (shoreTarget == null) return;

        // Sadece Z ekseninde hedef belirle. X ve Y mevcut pozisyonda kalsın.
        float targetZ = shoreTarget.position.z + formationOffset.z;
        Vector3 dest = new Vector3(transform.position.x, transform.position.y, targetZ);

        float distanceToTarget = Mathf.Abs(dest.z - transform.position.z);

        // Varış kontrolü (Sadece Z mesafesine bakıyoruz)
        if (distanceToTarget <= arrivalDistance)
        {
            HasArrived = true;
            currentSpeed = 0f;
            OnReachedShore?.Invoke();
            return;
        }

        // Gemi gibi yavaş yavaş hızlanma ve sona yaklaşınca yavaşlama
        float targetSpeed = MoveSpeed;
        
        // Basit bir yavaşlama mantığı (son 15 birimde yavaşla)
        if (distanceToTarget < 15f)
        {
            targetSpeed = Mathf.Lerp(0.2f, MoveSpeed, distanceToTarget / 15f);
        }

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);

        /* 
        // Dönüş mantığı devre dışı — Kullanıcı rotasyonun korunmasını istiyor
        Vector3 dir = dest - transform.position;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
        }
        */

        transform.position = Vector3.MoveTowards(transform.position, dest, currentSpeed * Time.deltaTime);
    }

    // WaveSpawner sahildeki hedef noktayı buradan verir.
    public void SetShoreTarget(Transform target) => shoreTarget = target;

    // Formasyon slotu — her gemi sahile farklı noktada varsın diye eklenir.
    public void SetFormationOffset(Vector3 offset) => formationOffset = offset;
}
