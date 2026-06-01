using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class UpgradePanelController : MonoBehaviour
{
    private VisualElement root;
    private Label headerLabel;
    private Label descriptionLabel;
    private VisualElement costList;
    private Button upgradeButton;
    private Button closeButton;

    private IUpgradeable currentTarget;
    private string currentTargetName;
    private UpgradeData nextUpgrade;

    private void OnEnable()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        root.style.display = DisplayStyle.None;

        headerLabel = root.Q<Label>("headerLabel");
        descriptionLabel = root.Q<Label>("descriptionLabel");
        costList = root.Q<VisualElement>("costList");
        upgradeButton = root.Q<Button>("upgradeButton");
        closeButton = root.Q<Button>("closeButton");

        upgradeButton.clicked += HandleUpgrade;
        closeButton.clicked += Close;

        EventBus.OnOpenUpgradeMenu += Open;
    }

    private void OnDisable()
    {
        EventBus.OnOpenUpgradeMenu -= Open;
    }

    public void Open(IUpgradeable target, string targetName)
    {
        currentTarget = target;
        currentTargetName = targetName;
        nextUpgrade = target.GetNextUpgrade();
        
        if (nextUpgrade == null) return;

        root.style.display = DisplayStyle.Flex;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;

        headerLabel.text = $"{targetName} Yükseltme";
        descriptionLabel.text = $"Seviye {nextUpgrade.fromLevel} -> {nextUpgrade.toLevel}";

        PopulateCosts();
        UpdateUpgradeButton();
    }

    private void PopulateCosts()
    {
        costList.Clear();
        if (nextUpgrade == null || nextUpgrade.cost == null) return;

        foreach (var cost in nextUpgrade.cost)
        {
            Label costLabel = new Label($"{cost.resourceType}: {cost.amount}");
            costLabel.AddToClassList("cost-item");
            costList.Add(costLabel);
        }
    }

    private void UpdateUpgradeButton()
    {
        if (nextUpgrade == null || EconomyManager.Instance == null)
        {
            upgradeButton.SetEnabled(false);
            return;
        }

        bool canAfford = true;
        foreach (var cost in nextUpgrade.cost)
        {
            if (!EconomyManager.Instance.HasEnough(cost.resourceType, cost.amount))
            {
                canAfford = false;
                break;
            }
        }
        upgradeButton.SetEnabled(canAfford);
    }

    private void HandleUpgrade()
    {
        if (currentTarget != null && nextUpgrade != null)
        {
            if (UpgradeManager.Instance != null)
            {
                UpgradeManager.Instance.TryStartUpgrade(currentTarget, currentTargetName);
            }
            Close();
        }
    }

    public void Close()
    {
        root.style.display = DisplayStyle.None;
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
        currentTarget = null;
    }
}
