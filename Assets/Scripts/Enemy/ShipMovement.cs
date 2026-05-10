using System;
using UnityEngine;

// Düşman gemisinin hareketi — kuzeyden (denizden) sahile doğru ilerler.
// Sahile varınca durur; saldırıyı ShipAttack devralsın diye OnReachedShore tetiklenir.
public class ShipMovement : MonoBehaviour
{
    [SerializeField] private ShipData shipData;
    [SerializeField] private Transform shoreTarget;     // Sahildeki iniş noktası — WaveSpawner bağlar
    [SerializeField] private float arrivalDistance = 1.5f;
    [SerializeField] private float turnSpeed = 4f;

    public bool HasArrived { get; private set; }
    public event Action OnReachedShore;

    private float MoveSpeed => shipData != null ? shipData.moveSpeed : 3f;

    private void OnEnable()
    {
        HasArrived = false;     // pool'dan tekrar kullanımda sıfırla
    }

    private void Update()
    {
        if (HasArrived) return;

        bool hasTarget = shoreTarget != null;
        Vector3 dest = hasTarget ? shoreTarget.position : transform.position + Vector3.back;
        dest.y = transform.position.y;      // su seviyesinde kal

        if (hasTarget && (dest - transform.position).sqrMagnitude <= arrivalDistance * arrivalDistance)
        {
            HasArrived = true;
            OnReachedShore?.Invoke();
            return;
        }

        Vector3 dir = dest - transform.position;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
        }
        transform.position = Vector3.MoveTowards(transform.position, dest, MoveSpeed * Time.deltaTime);
    }

    // WaveSpawner sahildeki hedef noktayı buradan verir.
    public void SetShoreTarget(Transform target) => shoreTarget = target;
}
