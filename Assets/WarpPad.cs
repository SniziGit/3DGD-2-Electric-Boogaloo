using UnityEngine;
using UnityEngine.SceneManagement;

public class WarpPad : MonoBehaviour, IInteractable
{
    [Header("Player Detection")]
    public PlayerDetector playerDetector;
    
    [Header("Warp Pad Settings")]
    public string interactionName = "Activate Warp Pad";
    public float interactionRange = 3f;
    public string mainMenuSceneName = "MainMenu";
    public float delayBeforeLoad = 2f;
    
    private bool gameEnded = false;
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
        if (!gameEnded && playerDetector != null && playerDetector.playerCount >= 2)
        {
            // Check if all collectables have been collected before allowing game end
            if (collectableManager != null && collectableManager.GetCurrentCollected() >= collectableManager.GetTotalRequired())
            {
                EndGame();
            }
            else
            {
                Debug.Log($"WarpPad: Players detected but only {collectableManager.GetCurrentCollected()}/{collectableManager.GetTotalRequired()} crystals collected!");
            }
        }
    }
    
    private void EndGame()
    {
        gameEnded = true;
        Debug.Log("Game Complete! 2 players reached the WarpPad with all crystals collected!");
        
        // Notify LevelManager that warp pad was reached
        if (levelManager != null)
        {
            levelManager.ReachWarpPad();
        }
        
        Invoke("LoadMainMenu", delayBeforeLoad);
    }
    
    private void LoadMainMenu()
    {
        Time.timeScale = 1f; // Resume time before loading
        SceneManager.LoadScene(mainMenuSceneName);
    }
    
    // IInteractable implementation
    public string GetInteractionName()
    {
        // Check if all collectables have been collected
        if (collectableManager != null && collectableManager.GetCurrentCollected() >= collectableManager.GetTotalRequired())
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
            
        return collectableManager.GetCurrentCollected() >= collectableManager.GetTotalRequired();
    }
    
    public void Interact(GameObject player)
    {
        // Double-check that all collectables have been collected
        if (collectableManager == null || collectableManager.GetCurrentCollected() < collectableManager.GetTotalRequired())
        {
            Debug.Log("WarpPad: Cannot interact - not all collectables collected!");
            return;
        }
        
        Debug.Log("WarpPad: Activating warp pad - all collectables collected!");
        
        // Notify LevelManager that warp pad was reached
        if (levelManager != null)
        {
            levelManager.ReachWarpPad();
        }
        
        // Load main menu scene
        if (!string.IsNullOrEmpty(mainMenuSceneName))
        {
            Time.timeScale = 1f; // Resume time before loading
            SceneManager.LoadScene(mainMenuSceneName);
        }
        else
        {
            Debug.LogError("WarpPad: Main menu scene name not set!");
        }
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
