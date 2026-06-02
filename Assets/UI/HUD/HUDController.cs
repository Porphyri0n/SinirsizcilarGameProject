using UnityEngine;
using UnityEngine.UIElements;

public class HUDController : MonoBehaviour
{
    private VisualElement root;
    private Label waveLabel;
    private Label timerLabel;
    private Label woodAmount;
    private Label stoneAmount;
    private Label ironAmount;
    private Label steelAmount;
    private Label goldAmount;
    private Label crystalAmount;
    private VisualElement playerHpFill;
private VisualElement playerEnergyFill;
    private VisualElement castleHpFill;
    private VisualElement wall1HpFill;
    private VisualElement wall2HpFill;
    private VisualElement wall3HpFill;
    private VisualElement wall4HpFill;

    private GameObject localPlayer;
    private PlayerHealth localHealth;
    private PlayerStamina localStamina;
    private PotionSystem localPotions;

    private VisualElement potionPanel;
    private VisualElement potionList;

    private void OnEnable()
    {
        root = GetComponent<UIDocument>().rootVisualElement;

        waveLabel = root.Q<Label>("waveLabel");
        timerLabel = root.Q<Label>("timerLabel");
        woodAmount = root.Q<Label>("woodAmount");
        stoneAmount = root.Q<Label>("stoneAmount");
        ironAmount = root.Q<Label>("ironAmount");
        steelAmount = root.Q<Label>("steelAmount");
        goldAmount = root.Q<Label>("goldAmount");
        crystalAmount = root.Q<Label>("crystalAmount");
        playerHpFill = root.Q<VisualElement>("playerHpFill");
        playerEnergyFill = root.Q<VisualElement>("playerEnergyFill");
        castleHpFill = root.Q<VisualElement>("castleHpFill");
        wall1HpFill = root.Q<VisualElement>("wall1HpFill");
        wall2HpFill = root.Q<VisualElement>("wall2HpFill");
        wall3HpFill = root.Q<VisualElement>("wall3HpFill");
        wall4HpFill = root.Q<VisualElement>("wall4HpFill");

        potionPanel = root.Q<VisualElement>("potionPanel");
        potionList = root.Q<VisualElement>("potionList");
        if (potionPanel != null) potionPanel.style.display = DisplayStyle.None;

        EventBus.OnPhaseChanged += HandlePhaseChanged;
        EventBus.OnWaveStart += HandleWaveStart;
        EventBus.OnCastleDamaged += HandleCastleDamaged;
        EventBus.OnWallHealthChanged += HandleWallHealthChanged;
        EventBus.OnResourceReceived += HandleResourceChanged;
        EventBus.OnResourceDeposited += HandleResourceChanged;

        // Initial update
        UpdateResources();
    }

    private void OnDisable()
    {
        EventBus.OnPhaseChanged -= HandlePhaseChanged;
        EventBus.OnWaveStart -= HandleWaveStart;
        EventBus.OnCastleDamaged -= HandleCastleDamaged;
        EventBus.OnWallHealthChanged -= HandleWallHealthChanged;
        EventBus.OnResourceReceived -= HandleResourceChanged;
        EventBus.OnResourceDeposited -= HandleResourceChanged;
    }

    private void HandleWallHealthChanged(int index, float current, float max)
    {
        VisualElement targetFill = index switch
        {
            0 => wall1HpFill,
            1 => wall2HpFill,
            2 => wall3HpFill,
            3 => wall4HpFill,
            _ => null
        };

        if (targetFill == null) return;

        float percent = (max > 0f) ? (current / max) * 100f : 0f;
        targetFill.style.width = new Length(percent, LengthUnit.Percent);
    }

    private void Update()
{
        if (GamePhaseController.Instance != null)
        {
            float timeLeft = GamePhaseController.Instance.PrepTimeLeft;
            if (timeLeft > 0)
            {
                timerLabel.style.display = DisplayStyle.Flex;
                timerLabel.text = $"HAZIRLIK: {Mathf.CeilToInt(timeLeft)}s";
            }
            else
            {
                timerLabel.style.display = DisplayStyle.None;
            }
        }

        // Cache local player components if needed
        if (localPlayer == null)
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in players)
            {
                var controller = p.GetComponent<PlayerController>();
                if (controller != null && controller.enabled)
                {
                    localPlayer = p;
                    localHealth = p.GetComponent<PlayerHealth>();
                    localStamina = p.GetComponent<PlayerStamina>();
                    localPotions = p.GetComponent<PotionSystem>();
                    break;
                }
            }
        }

        UpdatePlayerHealth();
        UpdatePlayerEnergy();
        UpdateActivePotions();
    }

    private void UpdateActivePotions()
    {
        if (potionPanel == null || potionList == null) return;

        if (localPotions == null)
        {
            potionPanel.style.display = DisplayStyle.None;
            return;
        }

        potionList.Clear();
        bool anyActive = false;

        if (localPotions.StrengthActive && localPotions.StrengthRemaining > 0f)
        {
            anyActive = true;
            Label lbl = new Label($"Güç: {Mathf.CeilToInt(localPotions.StrengthRemaining)}s");
            lbl.AddToClassList("potion-item");
            potionList.Add(lbl);
        }

        if (localPotions.HearingActive && localPotions.HearingRemaining > 0f)
        {
            anyActive = true;
            Label lbl = new Label($"İşitme: {Mathf.CeilToInt(localPotions.HearingRemaining)}s");
            lbl.AddToClassList("potion-item");
            potionList.Add(lbl);
        }

        if (localPotions.SpeedActive && localPotions.SpeedRemaining > 0f)
        {
            anyActive = true;
            Label lbl = new Label($"Hız: {Mathf.CeilToInt(localPotions.SpeedRemaining)}s");
            lbl.AddToClassList("potion-item");
            potionList.Add(lbl);
        }

        if (localPotions.RegenerationActive && localPotions.RegenerationRemaining > 0f)
        {
            anyActive = true;
            Label lbl = new Label($"Yenilenme: {Mathf.CeilToInt(localPotions.RegenerationRemaining)}s");
            lbl.AddToClassList("potion-item");
            potionList.Add(lbl);
        }

        potionPanel.style.display = anyActive ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void UpdatePlayerHealth()
    {
        if (localHealth != null && playerHpFill != null)
        {
            float percent = (localHealth.CurrentHealth / localHealth.MaxHealth) * 100f;
            playerHpFill.style.width = new Length(percent, LengthUnit.Percent);
        }
    }

    private void UpdatePlayerEnergy()
    {
        if (localStamina != null && playerEnergyFill != null)
        {
            float percent = localStamina.StaminaPercent * 100f;
            playerEnergyFill.style.width = new Length(percent, LengthUnit.Percent);
        }
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        if (phase == GamePhase.Prep && GamePhaseController.Instance != null)
        {
            waveLabel.text = $"DALGA {GamePhaseController.Instance.UpcomingWave} (HAZIRLIK)";
        }
    }

    private void HandleWaveStart(int waveNumber)
    {
        waveLabel.text = $"DALGA {waveNumber}";
    }

    private void HandleCastleDamaged(float current, float max)
    {
        float percent = (current / max) * 100f;
        castleHpFill.style.width = new Length(percent, LengthUnit.Percent);
    }

    private void HandleResourceChanged(ResourceType type, int amount)
    {
        UpdateResources();
    }

    private void UpdateResources()
    {
        if (EconomyManager.Instance == null) return;

        if (woodAmount != null) woodAmount.text = EconomyManager.Instance.GetStock(ResourceType.Wood).ToString();
        if (stoneAmount != null) stoneAmount.text = EconomyManager.Instance.GetStock(ResourceType.Stone).ToString();
        if (ironAmount != null) ironAmount.text = EconomyManager.Instance.GetStock(ResourceType.Iron).ToString();
        if (steelAmount != null) steelAmount.text = EconomyManager.Instance.GetStock(ResourceType.Steel).ToString();
        if (goldAmount != null) goldAmount.text = EconomyManager.Instance.GetStock(ResourceType.Gold).ToString();
        if (crystalAmount != null) crystalAmount.text = EconomyManager.Instance.GetStock(ResourceType.Crystal).ToString();
    }
}
