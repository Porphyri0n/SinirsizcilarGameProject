using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class InteractPromptUITK : MonoBehaviour
{
    [SerializeField] private float verticalOffset = 1.5f;
    [SerializeField] private LayerMask occlusionMask = ~0;
    [SerializeField] private float eyeHeight = 1.6f;

    private UIDocument uiDocument;
    private Label promptLabel;
    private VisualElement root;
    
    private IInteractable interactable;
    private Transform anchor;
    private Camera viewCamera;
    private GameObject localPlayer;

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
        interactable = GetComponentInParent<IInteractable>();
        anchor = transform.parent != null ? transform.parent : transform;
        
        // Try to get root immediately, but LateUpdate will also check
        TryInitializeUI();
    }

    private void OnEnable()
    {
        TryInitializeUI();
    }

    private void TryInitializeUI()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            root = uiDocument.rootVisualElement.Q<VisualElement>("promptRoot");
            promptLabel = uiDocument.rootVisualElement.Q<Label>("promptLabel");
            
            if (root != null)
                Hide();
        }
    }

    private void LateUpdate()
    {
        if (viewCamera == null)
            viewCamera = Camera.main;
        if (localPlayer == null)
            localPlayer = GameObject.FindGameObjectWithTag(GameConstants.TAG_PLAYER);

        if (root == null)
        {
            TryInitializeUI();
            if (root == null) return;
        }

        if (interactable == null || localPlayer == null || viewCamera == null)
        {
            Hide();
            return;
        }

        // Position in world space
        transform.position = anchor.position + Vector3.up * verticalOffset;

        float distance = Vector3.Distance(localPlayer.transform.position, anchor.position);
        bool canInteract = interactable.CanInteract(localPlayer);
        bool hasLoS = HasLineOfSight();
        
        bool visible = distance <= GameConstants.INTERACT_RANGE
            && canInteract
            && hasLoS;

        if (!visible)
        {
            if (root != null && root.style.display != DisplayStyle.None)
            {
                Debug.Log($"[InteractPrompt] Hiding prompt for {anchor.name}");
                Hide();
            }
            return;
        }

        if (promptLabel != null)
            promptLabel.text = interactable.GetInteractPrompt();
        
        if (root != null && root.style.display != DisplayStyle.Flex)
        {
            Debug.Log($"[InteractPrompt] Showing prompt for {anchor.name}: {promptLabel?.text}");
            Show();
        }

        // Billboard rotation: Face the camera
        // Note: For UITK world space, the panel faces the GameObject's forward (+Z)
        // To face the camera, forward should point from the panel to the camera, OR 
        // we use the camera's rotation.
        transform.rotation = Quaternion.LookRotation(transform.position - viewCamera.transform.position);
    }

    private bool HasLineOfSight()
    {
        if (localPlayer == null) return false;

        Vector3 origin = localPlayer.transform.position + Vector3.up * eyeHeight;
        Vector3 targetPos = anchor.position + Vector3.up * 0.5f; // Aim for middle of object
        Vector3 toTarget = targetPos - origin;
        float dist = toTarget.magnitude;
        
        if (dist < 0.5f) return true; // Too close to occlude

        // Use RaycastAll to handle cases where origin is inside player or hits other transparent things
        var hits = Physics.RaycastAll(origin, toTarget.normalized, dist, occlusionMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            // Skip player's own colliders
            if (hit.collider.transform == localPlayer.transform || hit.collider.transform.IsChildOf(localPlayer.transform))
                continue;

            // Check if we hit the interactable or one of its children
            if (hit.collider.transform == anchor || hit.collider.transform.IsChildOf(anchor))
                return true;

            // Check if what we hit is actually an interactable (e.g. hitting another interactable shouldn't block)
            var hitInteractable = hit.collider.GetComponentInParent<IInteractable>();
            if (hitInteractable != null)
                continue;

            // If we reached here, it's a solid object blocking the view
            return false;
        }

        return true;
    }

    private void Show()
    {
        if (root != null)
            root.style.display = DisplayStyle.Flex;
    }

    private void Hide()
    {
        if (root != null)
            root.style.display = DisplayStyle.None;
    }
}
