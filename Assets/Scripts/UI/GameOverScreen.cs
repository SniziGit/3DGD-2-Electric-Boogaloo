using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.Events;

public class GameOverScreen : MonoBehaviour
{
    [Header("UI References")]
    public Button[] quitButtons;
    public Button[] mainMenuButtons;

    [Header("Stats Display")]
    public TextMeshProUGUI timeTakenText;
    public TextMeshProUGUI player1HealthText;
    public TextMeshProUGUI player2HealthText;
    public TextMeshProUGUI crystalsCollectedText;

    [Header("Events")]
    public UnityEvent onWin;
    public UnityEvent onLose;

    private void Awake()
    {
        // Setup quit button listeners (for both win and lose screens)
        if (quitButtons != null)
        {
            for (int i = 0; i < quitButtons.Length; i++)
            {
                if (quitButtons[i] != null)
                {
                    quitButtons[i].onClick.AddListener(QuitApplication);
                }
            }
        }

        // Setup main menu button listeners (for both win and lose screens)
        if (mainMenuButtons != null)
        {
            for (int i = 0; i < mainMenuButtons.Length; i++)
            {
                if (mainMenuButtons[i] != null)
                {
                    mainMenuButtons[i].onClick.AddListener(GoToMainMenu);
                }
            }
        }
    }

    private void Start()
    {
        // GameOverScreen is now controlled directly by LevelManager
    }

    public void ShowWin()
    {
        UpdateStatsDisplay();
        DisablePlayerControls();
        
        // Only unlock cursor if it's currently locked
        if (Cursor.lockState != CursorLockMode.None)
        {
            UnlockCursor();
        }
        
        onWin?.Invoke();
    }

    public void ShowLose()
    {
        UpdateStatsDisplay();
        DisablePlayerControls();
        
        // Only unlock cursor if it's currently locked
        if (Cursor.lockState != CursorLockMode.None)
        {
            UnlockCursor();
        }
        
        onLose?.Invoke();
    }

    private void UpdateStatsDisplay()
    {
        if (LevelManager.Instance != null)
        {
            // Time taken
            if (timeTakenText != null)
            {
                float timeTaken = LevelManager.Instance.maxTime - LevelManager.Instance.GetTimerRemaining();
                var minutes = Mathf.FloorToInt(timeTaken / 60f);
                var seconds = Mathf.FloorToInt(timeTaken % 60f);
                timeTakenText.text = $"Time: {minutes:00}:{seconds:00}";
            }

            // Crystals collected
            if (crystalsCollectedText != null)
            {
                int collected = LevelManager.Instance.collectableManager?.GetCurrentCollected() ?? 0;
                int total = LevelManager.Instance.collectableManager?.GetTotalRequired() ?? 0;
                crystalsCollectedText.text = $"Crystals: {collected}/{total}";
            }
        }

        // Player health (separate for each player)
        UpdatePlayerHealthDisplay();
    }

    private void UpdatePlayerHealthDisplay()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        
        for (int i = 0; i < players.Length && i < 2; i++)
        {
            PlayerHealth playerHealth = players[i].GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                TextMeshProUGUI healthText = (i == 0) ? player1HealthText : player2HealthText;
                if (healthText != null)
                {
                    int currentHealth = playerHealth.GetCurrentHealth();
                    int maxHealth = playerHealth.GetMaxHealth();
                    healthText.text = $"Player {(i + 1)} Health: {currentHealth}/{maxHealth}";
                }
            }
        }
    }

    public void QuitApplication()
    {
        Time.timeScale = 1f; // Resume time before quitting
        Application.Quit();
        
        // Note: Application.Quit() doesn't work in the Unity Editor
        // It only works in built applications
        #if UNITY_EDITOR
        Debug.Log("Quit Application - This will only work in a build, not the editor");
        #endif
    }

    public void GoToMainMenu()
    {
        if (LoadingSceneManager.Instance != null)
        {
            LoadingSceneManager.Instance.SwitchToScene("MainMenu");
        }
        else
        {
            // Fallback to direct scene loading
            SceneManager.LoadScene("MainMenu");
        }
    }

    public void HideGameOver()
    {
        EnablePlayerControls();
        LockCursor();
    }
    
    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
            
            // Disable PauseManager
            PauseManager pauseManager = player.GetComponent<PauseManager>();
            if (pauseManager != null)
            {
                pauseManager.enabled = false;
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
            
            // Enable PauseManager
            PauseManager pauseManager = player.GetComponent<PauseManager>();
            if (pauseManager != null)
            {
                pauseManager.enabled = true;
            }
        }
    }
}
