using UnityEngine;
using UnityEngine.InputSystem;

public class PauseManager : MonoBehaviour
{
    [Header("Pause Settings")]
    [SerializeField] private GameObject pauseMenu;
    
    private PlayerInput playerInput;
    private InputAction pauseAction;
    private bool isPaused = false;

    void Start()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogError("PauseManager requires a PlayerInput component!");
            return;
        }
        
        // Find the pause action
        pauseAction = playerInput.actions.FindAction("Pause");
        if (pauseAction != null)
        {
            pauseAction.performed += TogglePause;
            pauseAction.Enable();
        }
        else
        {
            Debug.LogError("Pause action not found in PlayerInput actions!");
        }
        
        // Ensure pause menu starts hidden
        if (pauseMenu != null)
            pauseMenu.SetActive(false);
    }

    void OnDestroy()
    {
        if (pauseAction != null)
        {
            pauseAction.performed -= TogglePause;
            pauseAction.Disable();
        }
    }

    private void TogglePause(InputAction.CallbackContext context)
    {
        if (isPaused)
            UnpauseGame();
        else
            PauseGame();
    }

    private void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f; // Freeze the game
        
        if (pauseMenu != null)
            pauseMenu.SetActive(true);
            
        // Show and unlock cursor for menu interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        Debug.Log("Game Paused");
    }

    private void UnpauseGame()
    {
        isPaused = false;
        Time.timeScale = 1f; // Unfreeze the game
        
        if (pauseMenu != null)
            pauseMenu.SetActive(false);
            
        // Hide and lock cursor for gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        Debug.Log("Game Unpaused");
    }

    // Public method for UI buttons to call
    public void ResumeButton()
    {
        UnpauseGame();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
