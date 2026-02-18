using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InteractionPreview : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject interactionPanel;
    [SerializeField] private TextMeshProUGUI interactionText;
    [SerializeField] private Image progressBar;
    [SerializeField] private TextMeshProUGUI progressText;
    
    [Header("Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private string[] interactableTags = { "Interactable", "Pickup" };
    [SerializeField] private float raycastDistance = 5f;
    
    private Camera playerCamera;
    private FPSMovement playerMovement;
    private ReviveInteraction reviveInteraction;
    private PlayerHealth playerHealth;
    
    private IInteractable currentInteractable;
    private PlayerHealth currentReviveTarget;
    private GameObject currentTarget;
    
    private bool isReviving = false;
    private float reviveProgress = 0f;
    
    void Start()
    {
        playerCamera = GetComponentInChildren<Camera>();
        playerMovement = GetComponent<FPSMovement>();
        reviveInteraction = GetComponent<ReviveInteraction>();
        playerHealth = GetComponent<PlayerHealth>();
        
        // Hide UI initially
        if (interactionPanel != null)
            interactionPanel.SetActive(false);
    }
    
    void Update()
    {
        if (playerHealth != null && playerHealth.IsDowned())
        {
            HideInteractionUI();
            return;
        }
        
        CheckForInteractables();
        UpdateReviveProgress();
        UpdateUI();
    }
    
    void CheckForInteractables()
    {
        currentInteractable = null;
        currentReviveTarget = null;
        currentTarget = null;
        
        // Check for revive targets first (higher priority)
        if (reviveInteraction != null && reviveInteraction.CanRevive())
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            
            foreach (GameObject player in players)
            {
                if (player != gameObject)
                {
                    PlayerHealth targetHealth = player.GetComponent<PlayerHealth>();
                    if (targetHealth != null && targetHealth.IsDowned())
                    {
                        float distance = Vector3.Distance(transform.position, player.transform.position);
                        if (distance <= interactionRange)
                        {
                            // Check if looking at the player
                            Vector3 directionToTarget = (player.transform.position - transform.position).normalized;
                            float dotProduct = Vector3.Dot(playerCamera.transform.forward, directionToTarget);
                            
                            if (dotProduct > 0.5f) // Within ~60 degrees
                            {
                                currentReviveTarget = targetHealth;
                                currentTarget = player;
                                isReviving = reviveInteraction.IsReviving();
                                return;
                            }
                        }
                    }
                }
            }
        }
        
        // Check for other interactables using raycast
        RaycastHit hit;
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, raycastDistance))
        {
            float distance = Vector3.Distance(transform.position, hit.transform.position);
            
            if (distance <= interactionRange)
            {
                // Check if the hit object has an interactable tag
                if (IsInteractableTag(hit.collider.tag))
                {
                    IInteractable interactable = hit.collider.GetComponent<IInteractable>();
                    if (interactable != null && interactable.CanInteract(gameObject))
                    {
                        currentInteractable = interactable;
                        currentTarget = hit.collider.gameObject;
                    }
                }
            }
        }
        
        // Also check for objects with interactable tags without relying on raycast
        foreach (string tag in interactableTags)
        {
            GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(tag);
            foreach (GameObject obj in taggedObjects)
            {
                float distance = Vector3.Distance(transform.position, obj.transform.position);
                
                if (distance <= interactionRange)
                {
                    // Check if looking at object
                    Vector3 directionToObj = (obj.transform.position - transform.position).normalized;
                    float dotProduct = Vector3.Dot(playerCamera.transform.forward, directionToObj);
                    
                    if (dotProduct > 0.5f) // Within ~60 degrees
                    {
                        IInteractable interactable = obj.GetComponent<IInteractable>();
                        if (interactable != null && interactable.CanInteract(gameObject))
                        {
                            currentInteractable = interactable;
                            currentTarget = obj;
                            break;
                        }
                    }
                }
            }
        }
    }
    
    void UpdateReviveProgress()
    {
        if (currentReviveTarget != null && reviveInteraction != null)
        {
            reviveProgress = reviveInteraction.GetReviveProgress();
        }
        else
        {
            reviveProgress = 0f;
        }
    }
    
    void UpdateUI()
    {
        bool shouldShowUI = (currentInteractable != null || currentReviveTarget != null);
        
        if (interactionPanel != null)
        {
            interactionPanel.SetActive(shouldShowUI);
        }
        
        if (shouldShowUI && interactionText != null)
        {
            if (currentReviveTarget != null)
            {
                if (isReviving)
                {
                    interactionText.text = "Reviving...";
                }
                else
                {
                    interactionText.text = "F to Revive";
                }
            }
            else if (currentInteractable != null)
            {
                string itemName = currentInteractable.GetInteractionName();
                interactionText.text = $"F to {itemName}";
            }
        }
        
        // Update progress bar for reviving
        if (progressBar != null)
        {
            progressBar.gameObject.SetActive(currentReviveTarget != null && isReviving);
            if (currentReviveTarget != null && isReviving)
            {
                progressBar.fillAmount = reviveProgress;
            }
        }
        
        if (progressText != null)
        {
            progressText.gameObject.SetActive(currentReviveTarget != null && isReviving);
            if (currentReviveTarget != null && isReviving)
            {
                progressText.text = $"{Mathf.RoundToInt(reviveProgress * 100)}%";
            }
        }
    }
    
    void HideInteractionUI()
    {
        if (interactionPanel != null)
            interactionPanel.SetActive(false);
    }
    
    bool IsInteractableTag(string tag)
    {
        foreach (string interactableTag in interactableTags)
        {
            if (tag == interactableTag)
                return true;
        }
        return false;
    }
    
    // Public methods for external access
    public bool HasInteractionTarget()
    {
        return currentInteractable != null || currentReviveTarget != null;
    }
    
    public IInteractable GetCurrentInteractable()
    {
        return currentInteractable;
    }
    
    public PlayerHealth GetCurrentReviveTarget()
    {
        return currentReviveTarget;
    }
}
