using UnityEngine;
using UnityEngine.InputSystem;

public class PauseManager : MonoBehaviour
{
    [Header("Pause Settings")]
    [SerializeField] private GameObject pauseMenu;
    
    private PlayerInput playerInput;
    private InputAction pauseAction;
    private bool isPaused = false;
    
    // Singleton for easy access
    public static PauseManager Instance { get; private set; }

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

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
        // Don't allow pause/unpause if game is over
        if (LevelManager.Instance != null && LevelManager.Instance.isGameOver)
        {
            return;
        }
        
        if (isPaused)
            UnpauseGame();
        else
            PauseGame();
    }

    private void PauseGame()
    {
        isPaused = true;
        DisablePlayerControls();
        
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
        EnablePlayerControls();
        
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
    
    // Public property to check if game is paused
    public bool IsPaused()
    {
        return isPaused;
    }
    
    private void DisablePlayerControls()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        
        foreach (GameObject player in players)
        {
            // Disable FPSMovement
            FPSMovement fpsMovement = player.GetComponent<FPSMovement>();
            if (fpsMovement != null)
            {
                fpsMovement.enabled = false;
            }
            
            // Disable PlayerGun
            PlayerGun playerGun = player.GetComponentInChildren<PlayerGun>();
            if (playerGun != null)
            {
                playerGun.enabled = false;
            }
            
            // Disable PlayerShooting (input handler)
            PlayerShooting playerShooting = player.GetComponentInChildren<PlayerShooting>();
            if (playerShooting != null)
            {
                playerShooting.enabled = false;
            }
        }
    }
    
    private void EnablePlayerControls()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        
        foreach (GameObject player in players)
        {
            // Enable FPSMovement
            FPSMovement fpsMovement = player.GetComponent<FPSMovement>();
            if (fpsMovement != null)
            {
                fpsMovement.enabled = true;
            }
            
            // Enable PlayerGun
            PlayerGun playerGun = player.GetComponentInChildren<PlayerGun>();
            if (playerGun != null)
            {
                playerGun.enabled = true;
            }
            
            // Enable PlayerShooting (input handler)
            PlayerShooting playerShooting = player.GetComponentInChildren<PlayerShooting>();
            if (playerShooting != null)
            {
                playerShooting.enabled = true;
            }
        }
    }
}
