using System;
using UnityEngine;

// Ticari kervan hareketi — dogu/bati ticaret yolundan kaleye dogru ilerler.
// CaravanData.moveSpeed kullanir. Kaleye varinca OnReachedCastle, teslim sonrasi
// geri donup yol cikisina varinca OnDeparted tetiklenir.
public class CaravanMovement : MonoBehaviour
{
    [SerializeField] private CaravanData data;
    [SerializeField] private Transform castleTarget;        // Kaledeki teslim noktasi
    [SerializeField] private Transform exitTarget;          // Geri donulen yol cikisi
    [SerializeField] private float arrivalDistance = 1.5f;
    [SerializeField] private float turnSpeed = 4f;

    private Transform currentTarget;
    private bool moving;
    private bool returning;

    public bool HasArrived { get; private set; }
    public event Action OnReachedCastle;
    public event Action OnDeparted;

    private float MoveSpeed => data != null ? data.moveSpeed : 3f;

    public void Configure(CaravanData caravanData) => data = caravanData;
    public void SetCastleTarget(Transform t) => castleTarget = t;
    public void SetExitTarget(Transform t) => exitTarget = t;

    // Kaleye dogru yolculugu baslatir.
    public void BeginApproach()
    {
        currentTarget = castleTarget;
        moving = true;
        returning = false;
        HasArrived = false;
    }

    // Teslim sonrasi yol cikisina geri donus.
    public void BeginDepart()
    {
        currentTarget = exitTarget;
        moving = true;
        returning = true;
        HasArrived = false;
    }

    public void StopMoving() => moving = false;

    private void Update()
    {
        if (!moving || currentTarget == null) return;

        Vector3 dest = currentTarget.position;
        dest.y = transform.position.y;      // zemin seviyesinde kal

        if ((dest - transform.position).sqrMagnitude <= arrivalDistance * arrivalDistance)
        {
            moving = false;
            if (returning) OnDeparted?.Invoke();
            else { HasArrived = true; OnReachedCastle?.Invoke(); }
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
}
