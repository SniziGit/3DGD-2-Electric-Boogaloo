using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class HUDControl : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI collectibleText;
    public GameObject pausePanel;
    public GameObject instructionPanel;

    private void Update()
    {
        LevelManager levelManager = FindObjectOfType<LevelManager>();
        if (levelManager == null)
            return;

        // Handle pause toggle
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            bool newPaused = !levelManager.isPaused;
            levelManager.SetPaused(newPaused);

            if (pausePanel != null)
            {
                pausePanel.SetActive(newPaused);
            }
        }

        // Handle instruction panel toggle with H key
        if (Input.GetKeyDown(KeyCode.H) && instructionPanel != null)
        {
            bool currentState = instructionPanel.activeSelf;
            instructionPanel.SetActive(!currentState);
        }

        // Ensure pause panel reflects current state
        if (pausePanel != null)
        {
            pausePanel.SetActive(levelManager.isPaused);
        }


        // Update timer display (formatted as minutes:seconds) using integer seconds
        if (timerText != null)
        {
            int timeRemaining = levelManager.GetTimerRemaining();
            int minutes = timeRemaining / 60;
            int seconds = timeRemaining % 60;
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    // Called by UI button to resume the game
    public void ResumeGame()
    {
        LevelManager levelManager = FindObjectOfType<LevelManager>();
        if (levelManager == null)
            return;

        levelManager.SetPaused(false);

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    // Called by UI button to explicitly pause the game
    public void PauseGame()
    {
        LevelManager levelManager = FindObjectOfType<LevelManager>();
        if (levelManager == null)
            return;

        levelManager.SetPaused(true);

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }
    }

    public void ExitLevel()
    {
        // Load the title screen scene
        SceneManager.LoadScene("MainMenu");
    }

    public void RestartLevel()
    {
        // Reload the current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
