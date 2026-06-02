using UnityEngine;
using UnityEngine.UI;

public class WorldSpaceHealthBar : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Vector3 offset = new Vector3(0, 2.2f, 0);
    
    private IDamageable target;
    private Canvas canvas;
    private Camera mainCamera;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        mainCamera = Camera.main;
        target = GetComponentInParent<IDamageable>();
        
        if (healthSlider == null)
            healthSlider = GetComponentInChildren<Slider>();
    }

    private void OnEnable()
    {
        UpdateHealth();
    }

    private void LateUpdate()
    {
        if (target == null || !target.IsAlive)
        {
            if (canvas.enabled) canvas.enabled = false;
            return;
        }

        if (!canvas.enabled) canvas.enabled = true;

        // Position over target
        transform.position = transform.parent.position + offset;

        // Face camera
        if (mainCamera != null)
        {
            transform.rotation = mainCamera.transform.rotation;
        }

        UpdateHealth();
    }

    private void UpdateHealth()
    {
        if (target != null && healthSlider != null)
        {
            float targetValue = target.MaxHealth > 0 ? target.CurrentHealth / target.MaxHealth : 0;
            healthSlider.value = targetValue;
        }
    }
}
