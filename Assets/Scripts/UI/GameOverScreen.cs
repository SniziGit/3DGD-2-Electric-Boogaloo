using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameOverScreen : MonoBehaviour
{
    [Header("UI References")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI messageText;
    public Button restartButton;
    public Button exitButton;

    [Header("Messages")]
    [TextArea(3, 5)]
    public string[] winMessages = {
        "Excellent work! All bombs neutralized!"
    };

    [TextArea(3, 5)]
    public string[] outOfMovesMessages = {
        "No moves remaining... Try again!"
    };

    [TextArea(3, 5)]
    public string[] outOfTimeMessages = {
        "Too slow! Mission failed!",
    };

    private void Awake()
    {
        // Hide panel initially
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        // Setup button listeners
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartLevel);
        }

        if (exitButton != null)
        {
            exitButton.onClick.AddListener(ExitToTitle);
        }
    }

    private void Start()
    {
        // Listen for game over events
        LevelManager levelManager = FindObjectOfType<LevelManager>();
        if (levelManager != null)
        {
            // We'll check game over state in Update
        }
    }

    private void Update()
    {
        LevelManager levelManager = FindObjectOfType<LevelManager>();
        if (levelManager != null && levelManager.isGameOver && !gameOverPanel.activeInHierarchy)
        {
            ShowGameOver(levelManager.DidPlayerWin(), levelManager.IsOutOfMoves(), levelManager.IsOutOfTime());
        }
    }

    public void ShowGameOver(bool isWin, bool outOfMoves, bool outOfTime)
    {
        if (gameOverPanel == null || titleText == null || messageText == null)
        {
            Debug.LogWarning("GameOverScreen: Missing UI references!");
            return;
        }

        // Set title based on win/lose
        titleText.text = isWin ? "VICTORY!" : "GAME OVER";
        titleText.color = isWin ? Color.green : Color.red;

        // Choose appropriate message
        string message = "";
        if (isWin)
        {
            message = GetRandomMessage(winMessages);
        }
        else if (outOfMoves)
        {
            message = GetRandomMessage(outOfMovesMessages);
        }
        else if (outOfTime)
        {
            message = GetRandomMessage(outOfTimeMessages);
        }
        else
        {
            message = "Game Over!";
        }

        messageText.text = message;

        // Show the panel
        gameOverPanel.SetActive(true);

        // Pause the game when game over screen is shown
        Time.timeScale = 0f;
    }

    private string GetRandomMessage(string[] messages)
    {
        if (messages == null || messages.Length == 0)
            return "";

        return messages[Random.Range(0, messages.Length)];
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f; // Resume time before loading
        SceneManager.LoadScene("Game");
    }

    public void ExitToTitle()
    {
        Time.timeScale = 1f; // Resume time before loading
        SceneManager.LoadScene("Title");
    }

    public void HideGameOver()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
        Time.timeScale = 1f;
    }
}
