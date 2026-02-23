using UnityEngine;

public class WarpPad : MonoBehaviour, IInteractable
{
    [Header("Player Detection")]
    public PlayerDetector playerDetector;
    
    [Header("Warp Pad Settings")]
    public string interactionName = "Activate Warp Pad";
    public float interactionRange = 3f;
    
    private bool hasNotifiedPlayers = false;
    private LevelManager levelManager;
    private CollectableManager collectableManager;
    
    void Start()
    {
        if (playerDetector == null)
        {
            playerDetector = GetComponentInChildren<PlayerDetector>();
        }
        
        if (playerDetector == null)
        {
            Debug.LogError("WarpPad: PlayerDetector component not found!");
        }
        
        levelManager = FindObjectOfType<LevelManager>();
        collectableManager = CollectableManager.Instance;
        
        if (levelManager == null)
            Debug.LogWarning("WarpPad: LevelManager not found in scene!");
            
        if (collectableManager == null)
            Debug.LogWarning("WarpPad: CollectableManager not found in scene!");
    }

    void Update()
    {
        if (playerDetector != null && playerDetector.playerCount >= 2)
        {
            // Notify LevelManager that both players reached the warp pad
            if (levelManager != null)
            {
                levelManager.ReachWarpPad();
                Debug.Log("WarpPad: Both players reached the warp pad!");
            }
            
            // Show prompt if not all collectables are collected
            if (!hasNotifiedPlayers && collectableManager != null && !collectableManager.allCollected)
            {
                Debug.Log("WarpPad: Both players detected! Find all collectables to win!");
                hasNotifiedPlayers = true;
            }
        }
        else if (playerDetector != null && playerDetector.playerCount < 2)
        {
            // Reset notification when players leave
            hasNotifiedPlayers = false;
            
            // Reset warp pad status when players leave
            if (levelManager != null)
            {
                levelManager.LeaveWarpPad();
                Debug.Log("WarpPad: Players left the warp pad area.");
            }
        }
    }
    
    // IInteractable implementation
    public string GetInteractionName()
    {
        // Check if all collectables have been collected
        if (collectableManager != null && collectableManager.allCollected)
        {
            return interactionName;
        }
        else
        {
            int remaining = collectableManager != null ? collectableManager.GetRemaining() : 0;
            return $"Collect {remaining} more crystals";
        }
    }
    
    public bool CanInteract(GameObject player)
    {
        // Can only interact if all collectables have been collected
        if (collectableManager == null)
            return false;
            
        return collectableManager.allCollected;
    }
    
    public void Interact(GameObject player)
    {
        // WarpPad interaction is now handled automatically when both players reach it
        // This method is kept for IInteractable interface compliance
        Debug.Log("WarpPad: Interaction handled automatically when both players are present.");
    }
    
    public float GetInteractionRange()
    {
        return interactionRange;
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw interaction range in editor
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
