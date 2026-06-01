using UnityEngine;

// Fiziksel ganimet objesi — oyuncu yanına gidip [E] ile toplar.
// EconomyManager veya PotionSystem ile entegre çalışır.
[RequireComponent(typeof(Collider))]
public class LootObject : MonoBehaviour, IInteractable
{
    [Header("Ganimet Ayarları")]
    [SerializeField] private LootType lootType;
    
    [Header("Kaynak Ayarları (Eğer Resource ise)")]
    [SerializeField] private ResourceType resourceType = ResourceType.Wood;
    [SerializeField] private int amount = 5;
    
    [Header("İksir Ayarları (Eğer Potion ise)")]
    [SerializeField] private PotionData potionData;

    [Header("Görsel")]
    [SerializeField] private string interactPrompt = "[E] Ganimeti Topla";
    [SerializeField] private GameObject collectEffectPrefab;

    public string GetInteractPrompt() => interactPrompt;

    public bool CanInteract(GameObject player) => true;

    public void Interact(GameObject player)
    {
        Collect(player);
    }

    private void Collect(GameObject player)
    {
        if (lootType == LootType.Resource)
        {
            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.AddResource(resourceType, amount);
                Debug.Log($"[Loot] {amount} adet {resourceType} toplandı.");
            }
        }
        else if (lootType == LootType.Potion)
        {
            PotionSystem potionSystem = player.GetComponent<PotionSystem>();
            if (potionSystem != null && potionData != null)
            {
                potionSystem.UsePotion(potionData);
                Debug.Log($"[Loot] {potionData.name} iksiri kullanıldı.");
            }
        }

        // Efekt ve Ses
        if (collectEffectPrefab != null)
        {
            Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);
        }

        // Objeyi kaldır
        Destroy(gameObject);
    }

    // Harita üzerinde kolay fark edilmesi için ufak bir dönme efekti
    private void Update()
    {
        transform.Rotate(Vector3.up, 60f * Time.deltaTime);
    }
}
