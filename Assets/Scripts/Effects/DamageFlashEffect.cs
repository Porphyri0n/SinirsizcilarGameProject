using System.Collections;
using UnityEngine;

public class DamageFlashEffect : MonoBehaviour
{
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private float flashDuration = 0.15f;
    [SerializeField] private Renderer[] renderers;

    private Color[] originalColors;
    private Coroutine flashCoroutine;
    private IDamageable health;

    private void Awake()
    {
        health = GetComponent<IDamageable>();
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>();
        }

        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            // Note: This assumes the material has a _Color or _BaseColor property.
            // For standard/URP shaders, this is usually true.
            if (renderers[i].material.HasProperty("_Color"))
                originalColors[i] = renderers[i].material.color;
            else if (renderers[i].material.HasProperty("_BaseColor"))
                originalColors[i] = renderers[i].material.GetColor("_BaseColor");
            else
                originalColors[i] = Color.white;
        }
    }

    private void OnEnable()
    {
        if (health is BanditHealth banditHealth)
            banditHealth.OnHealthChanged += HandleHealthChanged;
        else if (health is ShipHealth shipHealth)
            shipHealth.OnHealthChanged += HandleHealthChanged;
        else if (health is CaravanController caravan)
            caravan.OnHealthChanged += HandleHealthChanged;
        else if (health is PlayerHealth playerHealth)
            playerHealth.OnHealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        if (health is BanditHealth banditHealth)
            banditHealth.OnHealthChanged -= HandleHealthChanged;
        else if (health is ShipHealth shipHealth)
            shipHealth.OnHealthChanged -= HandleHealthChanged;
        else if (health is CaravanController caravan)
            caravan.OnHealthChanged -= HandleHealthChanged;
        else if (health is PlayerHealth playerHealth)
            playerHealth.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(float current, float max)
    {
        // Only flash if taking damage (not healing)
        // We could track previous health but usually OnHealthChanged is called on damage.
        Flash();
    }

    public void Flash()
    {
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            SetColor(renderers[i], flashColor);
        }

        yield return new WaitForSeconds(flashDuration);

        for (int i = 0; i < renderers.Length; i++)
        {
            SetColor(renderers[i], originalColors[i]);
        }

        flashCoroutine = null;
    }

    private void SetColor(Renderer r, Color c)
    {
        if (r.material.HasProperty("_Color"))
            r.material.color = c;
        else if (r.material.HasProperty("_BaseColor"))
            r.material.SetColor("_BaseColor", c);
    }
}
