using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using Dissonance;

public class OptionsPanelController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private bool showOnStart = false;

    private VisualElement root;
    private DropdownField micDropdown;
    private DropdownField speakerDropdown;
    private DropdownField resDropdown;
    private Toggle vsyncToggle;
    private Button closeButton;

    private void OnEnable()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        var uiRoot = uiDocument.rootVisualElement;
        root = uiRoot.Q<VisualElement>("optionsRoot");
        micDropdown = uiRoot.Q<DropdownField>("micDropdown");
        speakerDropdown = uiRoot.Q<DropdownField>("speakerDropdown");
        resDropdown = uiRoot.Q<DropdownField>("resDropdown");
        vsyncToggle = uiRoot.Q<Toggle>("vsyncToggle");
        closeButton = uiRoot.Q<Button>("closeButton");

        if (closeButton != null)
            closeButton.clicked += ToggleOptions;

        if (micDropdown != null)
        {
            PopulateMicrophones();
            micDropdown.RegisterValueChangedCallback(evt => {
                if (VoiceChatManager.Instance != null)
                {
                    VoiceChatManager.Instance.SetMicrophone(evt.newValue);
                }
            });
        }

        if (speakerDropdown != null)
        {
            PopulateSpeakers();
            // Not: Unity natif olarak çıkış aygıtı değiştirmeyi desteklemez.
            // Bu alan şu an için görsel/placeholder amaçlıdır.
        }

        if (resDropdown != null)
        {
            PopulateResolutions();
            resDropdown.RegisterValueChangedCallback(evt => SetResolution(evt.newValue));
        }

        if (vsyncToggle != null)
        {
            vsyncToggle.value = QualitySettings.vSyncCount > 0;
            vsyncToggle.RegisterValueChangedCallback(evt => QualitySettings.vSyncCount = evt.newValue ? 1 : 0);
        }

        SetVisibility(showOnStart);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleOptions();
        }
    }

    private void ToggleOptions()
    {
        if (root == null) return;
        bool isVisible = root.style.display == DisplayStyle.Flex;
        SetVisibility(!isVisible);
    }

    private void SetVisibility(bool visible)
    {
        if (root == null) return;
        root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (visible)
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            PopulateMicrophones();
            PopulateResolutions();
            PopulateSpeakers();
        }
    }

    private void PopulateMicrophones()
    {
        if (micDropdown == null) return;

        string[] devicesArray = Microphone.devices;
        List<string> devices = devicesArray != null ? devicesArray.ToList() : new List<string>();
        micDropdown.choices = devices;

        if (VoiceChatManager.Instance != null)
        {
            string current = VoiceChatManager.Instance.GetCurrentMicrophone();
            if (!string.IsNullOrEmpty(current) && devices.Contains(current))
            {
                micDropdown.value = current;
            }
            else if (devices.Count > 0)
            {
                micDropdown.value = devices[0];
                VoiceChatManager.Instance.SetMicrophone(devices[0]);
            }
        }
    }

    private void PopulateSpeakers()
    {
        if (speakerDropdown == null) return;
        speakerDropdown.choices = new List<string> { "Sistem Varsayılanı" };
        speakerDropdown.value = "Sistem Varsayılanı";
    }

    private void PopulateResolutions()
    {
        if (resDropdown == null) return;

        var uniqueRes = Screen.resolutions
            .Select(r => new { r.width, r.height })
            .Distinct()
            .OrderByDescending(r => r.width)
            .ThenByDescending(r => r.height)
            .ToList();

        List<string> options = uniqueRes.Select(r => $"{r.width} x {r.height}").ToList();
        resDropdown.choices = options;

        string currentRes = $"{Screen.width} x {Screen.height}";
        if (options.Contains(currentRes))
        {
            resDropdown.value = currentRes;
        }
        else if (options.Count > 0)
        {
            resDropdown.value = options[0];
        }
    }

    private void SetResolution(string resString)
    {
        if (string.IsNullOrEmpty(resString)) return;
        string[] parts = resString.Split('x');
        if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int width) && int.TryParse(parts[1].Trim(), out int height))
        {
            Screen.SetResolution(width, height, Screen.fullScreen);
        }
    }
}
