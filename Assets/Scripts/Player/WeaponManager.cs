using System;
using UnityEngine;

// Oyuncunun elindeki silahları (kılıç + kalkan) tutar.
// Equip ile değişir ve EventBus.FireWeaponEquipped tetiklenir.
public class WeaponManager : MonoBehaviour
{
    [Header("Başlangıç (boş bırakılabilir)")]
    [SerializeField] private WeaponData startingSword;
    [SerializeField] private WeaponData startingShield;

    [Header("Sahne Bağları")]
    [SerializeField] private Transform swordSocket;     // Modelin bağlanacağı el
    [SerializeField] private Transform shieldSocket;

    [SerializeField] private int playerID = -1;

    private WeaponData sword;
    private WeaponData shield;
    private GameObject swordInstance;
    private GameObject shieldInstance;

    public WeaponData Sword => sword;
    public WeaponData Shield => shield;
    public bool HasSword => sword != null;
    public bool HasShield => shield != null;

    private void Start()
    {
        if (startingSword != null) Equip(startingSword);
        if (startingShield != null) Equip(startingShield);
    }

    // Silahı türüne göre yerleştirir, eskisini düşürür.
    public void Equip(WeaponData data)
    {
        if (data == null) return;

        if (data.weaponType == WeaponType.Sword)
        {
            sword = data;
            ReplaceInstance(ref swordInstance, data.prefab, swordSocket);
        }
        else if (data.weaponType == WeaponType.Shield)
        {
            shield = data;
            ReplaceInstance(ref shieldInstance, data.prefab, shieldSocket);
        }

        EventBus.FireWeaponEquipped(playerID, data.weaponType);
    }

    public void Unequip(WeaponType type)
    {
        if (type == WeaponType.Sword)
        {
            sword = null;
            DestroyInstance(ref swordInstance);
        }
        else
        {
            shield = null;
            DestroyInstance(ref shieldInstance);
        }
    }

    public void SetPlayerID(int id) => playerID = id;

    private void ReplaceInstance(ref GameObject current, GameObject prefab, Transform socket)
    {
        DestroyInstance(ref current);
        if (prefab == null || socket == null) return;
        current = Instantiate(prefab, socket);
        current.transform.localPosition = Vector3.zero;
        current.transform.localRotation = Quaternion.identity;
    }

    private void DestroyInstance(ref GameObject obj)
    {
        if (obj != null) Destroy(obj);
        obj = null;
    }
}
