using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.Events;

public class GameOverScreen : MonoBehaviour
{
    [Header("UI References")]
    public Button winQuitButton;
    public Button winMainMenuButton;
    public Button loseQuitButton;
    public Button loseMainMenuButton;

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
        // Setup win screen button listeners
        if (winQuitButton != null)
        {
            winQuitButton.onClick.AddListener(QuitApplication);
        }

        if (winMainMenuButton != null)
        {
            winMainMenuButton.onClick.AddListener(ExitToTitle);
        }

        // Setup lose screen button listeners
        if (loseQuitButton != null)
        {
            loseQuitButton.onClick.AddListener(QuitApplication);
        }

        if (loseMainMenuButton != null)
        {
            loseMainMenuButton.onClick.AddListener(ExitToTitle);
        }
    }

    private void Start()
    {
        // GameOverScreen is now controlled directly by LevelManager
    }

    public void ShowWin()
    {
        UpdateStatsDisplay();
        onWin?.Invoke();
        Time.timeScale = 0f;
    }

    public void ShowLose()
    {
        UpdateStatsDisplay();
        onLose?.Invoke();
        Time.timeScale = 0f;
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

    public void ExitToTitle()
    {
        Time.timeScale = 1f; // Resume time before loading
        SceneManager.LoadScene("MainMenu");
    }

    public void HideGameOver()
    {
        Time.timeScale = 1f;
    }
}
