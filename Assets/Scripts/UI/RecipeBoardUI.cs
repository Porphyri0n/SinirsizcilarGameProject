using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Craft ocağının yanındaki dünya içi (diegetic) tarif tahtası.
// RecipeData listesini okur; her satırda icon + malzeme + çıktı gösterir.
// Renk kodu: yeterli kaynak yeşil, yetersiz kırmızı, ocak seviyesi yetmiyorsa gri kilitli.
// DiegeticUIManager'a kayıt olur — mesafeye göre görünürlük yönetilir.
public class RecipeBoardUI : MonoBehaviour, IDiegeticUI
{
    [Header("Tarif Listesi")]
    [SerializeField] private RecipeData[] recipes;
    [SerializeField] private CraftingStation station;       // Opsiyonel: seviye filtresi için

    [Header("Diegetic")]
    [SerializeField] private Transform anchor;
    [SerializeField] private float visibleRange = 18f;
    [SerializeField] private GameObject visualRoot;         // Tahta görseli — Set Visibility ile aç/kapa

    [Header("Slotlar")]
    [SerializeField] private RecipeSlot[] slots;
    [SerializeField] private float refreshInterval = 0.3f;

    [Header("Renkler")]
    [SerializeField] private Color affordableColor = new Color(0.35f, 0.85f, 0.35f);
    [SerializeField] private Color unaffordableColor = new Color(0.85f, 0.3f, 0.3f);
    [SerializeField] private Color lockedColor = new Color(0.45f, 0.45f, 0.45f);

    private float nextRefreshAt;
    private bool currentlyVisible;

    public Transform Anchor => anchor != null ? anchor : transform;
    public float VisibleRange => visibleRange;

    private void OnEnable()
    {
        EventBus.OnResourceReceived += HandleResourceChanged;
        EventBus.OnResourceDeposited += HandleResourceChanged;
        EventBus.OnUpgradeCompleted += HandleUpgradeCompleted;
        EventBus.OnCraftCompleted += HandleCraftCompleted;

        if (DiegeticUIManager.Instance != null)
            DiegeticUIManager.Instance.Register(this);

        BindSlots();
        Refresh();
    }

    private void OnDisable()
    {
        EventBus.OnResourceReceived -= HandleResourceChanged;
        EventBus.OnResourceDeposited -= HandleResourceChanged;
        EventBus.OnUpgradeCompleted -= HandleUpgradeCompleted;
        EventBus.OnCraftCompleted -= HandleCraftCompleted;

        if (DiegeticUIManager.Instance != null)
            DiegeticUIManager.Instance.Unregister(this);
    }

    private void Update()
    {
        if (!currentlyVisible) return;
        if (Time.time < nextRefreshAt) return;
        nextRefreshAt = Time.time + refreshInterval;
        Refresh();
    }

    private void BindSlots()
    {
        if (slots == null) return;

        int n = recipes != null ? Mathf.Min(slots.Length, recipes.Length) : 0;
        for (int i = 0; i < n; i++)
            slots[i].Bind(recipes[i]);
        for (int i = n; i < slots.Length; i++)
            slots[i].Bind(null);
    }

    private void Refresh()
    {
        if (slots == null) return;

        EconomyManager econ = EconomyManager.Instance;
        UpgradeLevel stationLevel = station != null ? station.CurrentLevel : UpgradeLevel.Tier3;

        foreach (RecipeSlot s in slots)
        {
            if (s == null) continue;
            s.Refresh(econ, stationLevel, affordableColor, unaffordableColor, lockedColor);
        }
    }

    private void HandleResourceChanged(ResourceType type, int amount) => nextRefreshAt = 0f;
    private void HandleUpgradeCompleted(string target, UpgradeLevel level) => nextRefreshAt = 0f;
    private void HandleCraftCompleted(RecipeData recipe) => nextRefreshAt = 0f;

    // ── IDiegeticUI ──────────────────────────────────────────────────────
    public void SetVisibility(bool visible)
    {
        currentlyVisible = visible;
        if (visualRoot != null) visualRoot.SetActive(visible);
        if (visible) Refresh();
    }

    // Tahtadaki tek tarif satırı.
    [Serializable]
    public class RecipeSlot
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Image background;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text ingredientsLabel;
        [SerializeField] private TMP_Text outputLabel;
        [SerializeField] private GameObject lockedOverlay;

        private RecipeData recipe;

        public void Bind(RecipeData data)
        {
            recipe = data;
            if (root != null) root.SetActive(data != null);
            if (data == null) return;

            if (iconImage != null) iconImage.sprite = data.recipeIcon;
            if (titleLabel != null) titleLabel.text = data.recipeName;
            if (ingredientsLabel != null) ingredientsLabel.text = BuildIngredients(data);
            if (outputLabel != null) outputLabel.text = BuildOutput(data);
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

        private string BuildIngredients(RecipeData data)
        {
            if (data.ingredients == null || data.ingredients.Length == 0) return string.Empty;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < data.ingredients.Length; i++)
            {
                RecipeIngredient ing = data.ingredients[i];
                if (ing == null) continue;
                if (i > 0) sb.Append(" + ");
                sb.Append(ing.resourceType);
                sb.Append("x");
                sb.Append(ing.amount);
            }
            return sb.ToString();
        }

        private string BuildOutput(RecipeData data)
        {
            if (data.outputWeapon.HasValue) return data.outputWeapon.Value.ToString();
            if (data.outputDefense.HasValue) return data.outputDefense.Value.ToString();
            return string.Empty;
        }
    }
}
