using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// 3D recipe tahtası — craft ocağına yaklaşınca aktif olur, tarifleri listeler.
// Renk kodu: yeterli kaynak yeşil, yetersiz kırmızı, ocak seviyesi yetmiyorsa kilitli (gri).
// Slot'lar Inspector'dan bağlanır. EconomyManager ve CraftingStation'a bakar.
public class CraftingUI : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private CraftingStation station;
    [SerializeField] private GameObject boardRoot;          // 3D tahta görseli (aç/kapa için)
    [SerializeField] private Transform playerProbe;         // proximity için (yoksa Player tag'inden bulunur)

    [Header("Etkileşim")]
    [SerializeField] private float showDistance = 4f;
    [SerializeField] private float refreshInterval = 0.25f;

    [Header("Renkler")]
    [SerializeField] private Color affordableColor = new Color(0.35f, 0.85f, 0.35f);
    [SerializeField] private Color unaffordableColor = new Color(0.85f, 0.3f, 0.3f);
    [SerializeField] private Color lockedColor = new Color(0.45f, 0.45f, 0.45f);

    [Header("Slotlar")]
    [SerializeField] private RecipeSlot[] slots;

    private Transform playerCache;
    private float nextRefreshAt;
    private bool boardVisible;

    private void OnEnable()
    {
        EventBus.OnResourceReceived += HandleResourceChanged;
        EventBus.OnResourceDeposited += HandleResourceChanged;
        EventBus.OnUpgradeCompleted += HandleUpgradeCompleted;
        SetBoardVisible(false);
        BindSlots();
    }

    private void OnDisable()
    {
        EventBus.OnResourceReceived -= HandleResourceChanged;
        EventBus.OnResourceDeposited -= HandleResourceChanged;
        EventBus.OnUpgradeCompleted -= HandleUpgradeCompleted;
    }

    private void Update()
    {
        UpdateVisibility();

        if (boardVisible && Time.time >= nextRefreshAt)
        {
            nextRefreshAt = Time.time + refreshInterval;
            RefreshSlots();
        }
    }

    private void UpdateVisibility()
    {
        Transform player = GetPlayer();
        if (player == null || station == null)
        {
            SetBoardVisible(false);
            return;
        }

        float sqr = (player.position - transform.position).sqrMagnitude;
        bool inRange = sqr <= showDistance * showDistance;
        if (inRange != boardVisible)
        {
            SetBoardVisible(inRange);
            if (inRange) RefreshSlots();   // açılır açılmaz güncel görünsün
        }
    }

    private Transform GetPlayer()
    {
        if (playerProbe != null) return playerProbe;
        if (playerCache != null) return playerCache;

        GameObject p = GameObject.FindGameObjectWithTag(GameConstants.TAG_PLAYER);
        if (p != null) playerCache = p.transform;
        return playerCache;
    }

    private void SetBoardVisible(bool visible)
    {
        boardVisible = visible;
        if (boardRoot != null) boardRoot.SetActive(visible);
    }

    private void BindSlots()
    {
        if (slots == null || station == null) return;

        IReadOnlyList<RecipeData> recipes = station.Recipes;
        if (recipes == null) return;

        int n = Mathf.Min(slots.Length, recipes.Count);
        for (int i = 0; i < n; i++)
            slots[i].Bind(recipes[i]);

        // Fazla slot varsa kapat
        for (int i = n; i < slots.Length; i++)
            slots[i].Bind(null);
    }

    private void RefreshSlots()
    {
        if (slots == null || station == null) return;

        EconomyManager econ = EconomyManager.Instance;
        UpgradeLevel stationLevel = station.CurrentLevel;

        foreach (RecipeSlot slot in slots)
        {
            if (slot == null) continue;
            slot.Refresh(econ, stationLevel, affordableColor, unaffordableColor, lockedColor);
        }
    }

    private void HandleResourceChanged(ResourceType type, int amount) => nextRefreshAt = 0f;
    private void HandleUpgradeCompleted(string target, UpgradeLevel level) => nextRefreshAt = 0f;

    // Tahtadaki tek bir tarif satırı.
    [Serializable]
    public class RecipeSlot
    {
        [SerializeField] private GameObject root;           // satırın görsel kökü (kilitlide kapatılabilir veya sadece renk değişir)
        [SerializeField] private Image background;          // arka plan renk kodu için
        [SerializeField] private Image iconImage;           // çıktı / tarif iconu
        [SerializeField] private Text labelText;            // tarif adı + malzeme listesi
        [SerializeField] private GameObject lockedOverlay;  // kilit ikonu (opsiyonel)

        private RecipeData recipe;

        public void Bind(RecipeData data)
        {
            recipe = data;
            if (root != null) root.SetActive(data != null);
            if (data == null) return;

            if (iconImage != null) iconImage.sprite = data.recipeIcon;
            if (labelText != null) labelText.text = BuildLabel(data);
        }

        public void Refresh(EconomyManager econ, UpgradeLevel stationLevel,
                            Color affordable, Color unaffordable, Color locked)
        {
            if (recipe == null) return;

            bool stationOk = (int)recipe.requiredStationLevel <= (int)stationLevel;
            bool canAfford = stationOk && HasIngredients(econ);

            if (lockedOverlay != null) lockedOverlay.SetActive(!stationOk);

            if (background != null)
            {
                if (!stationOk) background.color = locked;
                else if (canAfford) background.color = affordable;
                else background.color = unaffordable;
            }
        }

        private bool HasIngredients(EconomyManager econ)
        {
            if (recipe.ingredients == null) return true;
            if (econ == null) return false;

            foreach (RecipeIngredient ing in recipe.ingredients)
            {
                if (ing == null) continue;
                if (!econ.HasEnough(ing.resourceType, ing.amount)) return false;
            }
            return true;
        }

        private string BuildLabel(RecipeData data)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(data.recipeName);
            if (data.ingredients != null && data.ingredients.Length > 0)
            {
                sb.Append("\n");
                for (int i = 0; i < data.ingredients.Length; i++)
                {
                    RecipeIngredient ing = data.ingredients[i];
                    if (ing == null) continue;
                    if (i > 0) sb.Append(" + ");
                    sb.Append(ing.resourceType.ToString());
                    sb.Append("x");
                    sb.Append(ing.amount);
                }
            }
            return sb.ToString();
        }
    }
}
