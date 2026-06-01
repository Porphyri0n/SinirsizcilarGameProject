using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class DefeatScreenController : MonoBehaviour
{
    private VisualElement root;
    private Label survivedWavesLabel;
    private Button restartButton;
    private Button menuButton;

    private void OnEnable()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        root.style.display = DisplayStyle.None; // Start hidden

        survivedWavesLabel = root.Q<Label>("survivedWavesLabel");
        restartButton = root.Q<Button>("restartButton");
        menuButton = root.Q<Button>("menuButton");

        restartButton.clicked += HandleRestart;
        menuButton.clicked += HandleMenu;

        EventBus.OnGameLost += HandleGameLost;
    }

    private void OnDisable()
    {
        EventBus.OnGameLost -= HandleGameLost;
    }

    private void HandleGameLost(int survivedWaves)
    {
        root.style.display = DisplayStyle.Flex;
        survivedWavesLabel.text = $"Hayatta Kalınan Dalga: {survivedWaves}";
        
        // Unlock cursor
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
    }

    private void HandleRestart()
    {
        // Full restart via controller
        if (GameRestartController.Instance != null)
        {
            GameRestartController.Instance.RestartGame();
        }
        else
        {
            // Fallback if no controller
            EventBus.FireGameRestart();
        }
        
        root.style.display = DisplayStyle.None;
    }

    private void HandleMenu()
    {
        // Go to main menu (assuming it's scene 0 or named "Menu")
        SceneManager.LoadScene(0);
    }
}
