using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class CraftingMenuController : MonoBehaviour
{
    private VisualElement root;
    private ScrollView recipeScrollView;
    private VisualElement ingredientList;
    private Label recipeTitle;
    private VisualElement recipeIcon;
    private Button craftButton;
    private Button closeButton;

    private CraftingStation currentStation;
    private RecipeData selectedRecipe;

    private VisualElement craftingRoot;
    private int openFrame = -1;

    private void OnEnable()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        craftingRoot = root.Q<VisualElement>("craftingRoot");
        
        if (craftingRoot != null) craftingRoot.style.display = DisplayStyle.None;

        recipeScrollView = root.Q<ScrollView>("recipeScrollView");
        ingredientList = root.Q<VisualElement>("ingredientList");
        recipeTitle = root.Q<Label>("recipeTitle");
        recipeIcon = root.Q<VisualElement>("recipeIcon");
        craftButton = root.Q<Button>("craftButton");
        closeButton = root.Q<Button>("closeButton");

        if (craftButton != null) craftButton.clicked += HandleCraft;
        if (closeButton != null) closeButton.clicked += CloseMenu;
        
        EventBus.OnOpenCraftingMenu += OpenMenu;
        EventBus.OnResourceReceived += HandleResourceChanged;
        EventBus.OnResourceDeposited += HandleResourceChanged;
    }

    private void OnDisable()
    {
        if (craftButton != null) craftButton.clicked -= HandleCraft;
        if (closeButton != null) closeButton.clicked -= CloseMenu;
        EventBus.OnOpenCraftingMenu -= OpenMenu;
        EventBus.OnResourceReceived -= HandleResourceChanged;
        EventBus.OnResourceDeposited -= HandleResourceChanged;
    }

    private void HandleResourceChanged(ResourceType type, int amount)
    {
        if (craftingRoot != null && craftingRoot.style.display == DisplayStyle.Flex)
        {
            UpdateCraftButtonState();
        }
    }

    public void OpenMenu(CraftingStation station)
    {
        if (station == null) return;
        Debug.Log($"[CraftingMenuController] Opening for: {station.name}");
        currentStation = station;
        if (craftingRoot != null) craftingRoot.style.display = DisplayStyle.Flex;
        
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
        
        openFrame = Time.frameCount;
        PopulateRecipes();
    }

    private void Update()
    {
        if (craftingRoot != null && craftingRoot.style.display == DisplayStyle.Flex && Time.frameCount != openFrame)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
            {
                CloseMenu();
            }
        }
    }

    public void CloseMenu()
    {
        Debug.Log("[CraftingMenuController] Closing.");
        if (craftingRoot != null) craftingRoot.style.display = DisplayStyle.None;
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
        currentStation = null;
    }

    private void PopulateRecipes()
    {
        recipeScrollView.Clear();
        if (currentStation == null) return;

        var recipes = currentStation.GetCraftableRecipes().Where(r => r.outputPotion != null).ToArray();
        foreach (var recipe in recipes)
        {
            Button recipeBtn = new Button();
            recipeBtn.text = recipe.recipeName;
            recipeBtn.AddToClassList("recipe-item");
            recipeBtn.clicked += () => SelectRecipe(recipe);
            recipeScrollView.Add(recipeBtn);
        }

        if (recipes.Length > 0)
            SelectRecipe(recipes[0]);
    }

    private void SelectRecipe(RecipeData recipe)
    {
        selectedRecipe = recipe;
        recipeTitle.text = recipe.recipeName;
        recipeIcon.style.backgroundImage = new StyleBackground(recipe.recipeIcon);

        ingredientList.Clear();
        foreach (var ing in recipe.ingredients)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ingredient-item");
            
            Label nameLabel = new Label(ing.resourceType.ToString());
            nameLabel.AddToClassList("label");
            
            Label amountLabel = new Label($"x{ing.amount}");
            amountLabel.AddToClassList("label");

            row.Add(nameLabel);
            row.Add(amountLabel);
            ingredientList.Add(row);
        }

        UpdateCraftButtonState();
    }

    private void UpdateCraftButtonState()
    {
        if (selectedRecipe == null || currentStation == null || EconomyManager.Instance == null)
        {
            craftButton.SetEnabled(false);
            return;
        }

        bool canAfford = currentStation.CanAfford(selectedRecipe);
        craftButton.SetEnabled(canAfford);
    }

    private void HandleCraft()
    {
        if (selectedRecipe != null && currentStation != null)
        {
            if (currentStation.TryCraft(selectedRecipe))
            {
                Debug.Log($"[CraftingMenuController] Successfully requested craft: {selectedRecipe.recipeName}");
                UpdateCraftButtonState();
            }
        }
    }
}
