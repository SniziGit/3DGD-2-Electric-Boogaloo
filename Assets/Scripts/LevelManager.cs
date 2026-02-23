using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;

public class LevelManager : MonoBehaviour
{
    [Header("Game State")]
    public bool isGameOver = false;
    public bool isPaused = false;
    
    [Header("Timer")]
    public float maxTime = 480f; // 8 minutes default
    private float currentTime;
    
    public enum GameOverReason { Victory, OutOfTime, PlayersDead }
    
    [Header("Game Conditions")]
    public bool playerWon = false;
    public bool outOfTime = false;
    public bool playersDead = false;
    
    [Header("Scene Loading")]
    public string mainMenuSceneName = "MainMenu";
    public float delayBeforeLoad = 2f;
    
    [Header("UI References")]
    public GameOverScreen gameOverScreen;
    public TextMeshProUGUI timerText;

    [Header("Game Progress")]
    private CollectableManager collectableManager;
    public bool reachedWarpPad = false;
    
    // Player tracking (moved from GameManager)
    private List<PlayerHealth> players = new List<PlayerHealth>();
    private int downedPlayerCount = 0;
    
    private int player1DownedCount = 0;
    private int player2DownedCount = 0;
    private const int maxDownedCount = 3; // Players die after being downed 3 times
    
    private void Start()
    {
        currentTime = maxTime;
        collectableManager = CollectableManager.Instance;
        
        // Find GameOverScreen if not assigned
        if (gameOverScreen == null)
        {
            gameOverScreen = FindObjectOfType<GameOverScreen>();
        }
        
        // Don't initialize players here - they may not be spawned yet
        // Players will be added dynamically when CheckPlayerDowned is called
    }
    
    private void InitializePlayers()
    {
        // Find all players with "Player" tag
        GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
        
        Debug.Log($"Found {playerObjects.Length} GameObjects with 'Player' tag");
        
        foreach (GameObject playerObj in playerObjects)
        {
            PlayerHealth playerHealth = playerObj.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                players.Add(playerHealth);
                Debug.Log($"Added player: {playerObj.name} with PlayerHealth component");
            }
            else
            {
                Debug.LogWarning($"Found player object {playerObj.name} but no PlayerHealth component!");
            }
        }
        
        Debug.Log($"Total players initialized: {players.Count}");
    }
    
    private void Update()
    {
        if (isGameOver || isPaused) return;
        
        UpdateTimer();
        CheckWinConditions();
        CheckLoseConditions();
    }
    
    private void UpdateTimer()
    {
        currentTime -= Time.deltaTime;
        var minutes = Mathf.FloorToInt(currentTime / 60f);
        var seconds = Mathf.FloorToInt(currentTime % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";

        if (currentTime <= 0f)
        {
            currentTime = 0f;
            EndGame(GameOverReason.OutOfTime);
        }
    }
    
    private void CheckWinConditions()
    {
        if (collectableManager != null && collectableManager.allCollected && reachedWarpPad)
        {
            EndGame(GameOverReason.Victory);
        }
    }
    
    private void CheckLoseConditions()
    {
        if (playersDead)
        {
            EndGame(GameOverReason.PlayersDead);
        }
    }
    
    public bool DidPlayerWin()
    {
        return playerWon;
    }
    
    public bool IsOutOfTime()
    {
        return outOfTime;
    }
    
    public void SetPaused(bool paused)
    {
        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
    }
    
    public int GetTimerRemaining()
    {
        return Mathf.CeilToInt(currentTime);
    }
    
    public void TriggerGameOver(bool win)
    {
        EndGame(win ? GameOverReason.Victory : GameOverReason.PlayersDead);
    }
    
    private void EndGame(GameOverReason reason)
    {
        if (isGameOver) return;
        
        isGameOver = true;
        
        switch (reason)
        {
            case GameOverReason.Victory:
                playerWon = true;
                Debug.Log("Victory! All collectables gathered and warp pad reached!");
                ShowGameOverScreen(true, false, false);
                break;
            case GameOverReason.OutOfTime:
                outOfTime = true;
                Debug.Log("Game Over! Out of time!");
                ShowGameOverScreen(false, false, true);
                break;
            case GameOverReason.PlayersDead:
                playersDead = true;
                Debug.Log("Game Over! All players eliminated!");
                ShowGameOverScreen(false, true, false);
                break;
        }
        
        LoadMainMenuAfterDelay();
    }
    
    private void ShowGameOverScreen(bool isWin, bool outOfMoves, bool outOfTime)
    {
        if (gameOverScreen != null)
        {
            gameOverScreen.ShowGameOver(isWin, outOfMoves, outOfTime);
        }
        else
        {
            Debug.LogWarning("GameOverScreen not found!");
        }
    }
    
    private void LoadMainMenuAfterDelay()
    {
        Invoke(nameof(LoadMainMenu), delayBeforeLoad);
    }
    
    private void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
    
    // Collectible progress using CollectableManager
    public float GetCollectibleProgress()
    {
        if (collectableManager == null) return 0f;
        return (float)collectableManager.GetCurrentCollected() / collectableManager.GetTotalRequired();
    }
    
    // Warp pad management
    public void ReachWarpPad()
    {
        reachedWarpPad = true;
    }
    
    // Player downed state management (moved from GameManager)
    public void CheckPlayerDowned(PlayerHealth downedPlayer)
    {
        Debug.Log($"CheckPlayerDowned called for player: {downedPlayer.gameObject.name}");
        
        // Refresh the players list to find all current players
        RefreshPlayersList();
        
        // Count how many players are downed
        downedPlayerCount = 0;
        foreach (PlayerHealth player in players)
        {
            Debug.Log($"Checking player {player.gameObject.name}: IsDowned = {player.IsDowned()}");
            if (player.IsDowned())
            {
                downedPlayerCount++;
            }
        }
        
        Debug.Log($"Player downed check: {downedPlayerCount}/{players.Count} players downed");
        
        // Check if all players are downed
        if (downedPlayerCount >= players.Count && players.Count > 0)
        {
            Debug.Log("All players are downed! Triggering game over.");
            AllPlayersDowned();
        }
        else
        {
            Debug.Log("Not all players are downed yet. Game continues.");
        }
    }
    
    private void RefreshPlayersList()
    {
        // Clear current list and find all players again
        players.Clear();
        
        GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
        Debug.Log($"RefreshPlayersList: Found {playerObjects.Length} GameObjects with 'Player' tag");
        
        foreach (GameObject playerObj in playerObjects)
        {
            PlayerHealth playerHealth = playerObj.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                players.Add(playerHealth);
                Debug.Log($"Added player: {playerObj.name} with PlayerHealth component");
            }
            else
            {
                Debug.LogWarning($"Found player object {playerObj.name} but no PlayerHealth component!");
            }
        }
        
        Debug.Log($"RefreshPlayersList: Total players now tracked: {players.Count}");
    }
    
    public void PlayerRevived(PlayerHealth revivedPlayer)
    {
        Debug.Log("Player revived! Updating game state.");
        
        // Recalculate downed player count
        downedPlayerCount = 0;
        foreach (PlayerHealth player in players)
        {
            if (player.IsDowned())
            {
                downedPlayerCount++;
            }
        }
        
        Debug.Log($"Players downed after revive: {downedPlayerCount}/{players.Count}");
    }
    
    // Called when all players are downed
    private void AllPlayersDowned()
    {
        playersDead = true;
        EndGame(GameOverReason.PlayersDead);
    }
    
    // Legacy downed count management (for individual player tracking)
    public void PlayerDowned(int playerNumber)
    {
        if (playerNumber == 1)
        {
            player1DownedCount++;
            if (player1DownedCount >= maxDownedCount && player2DownedCount >= maxDownedCount)
            {
                playersDead = true;
            }
        }
        else if (playerNumber == 2)
        {
            player2DownedCount++;
            if (player1DownedCount >= maxDownedCount && player2DownedCount >= maxDownedCount)
            {
                playersDead = true;
            }
        }
    }
    
    public bool IsPlayerDead(int playerNumber)
    {
        if (playerNumber == 1)
            return player1DownedCount >= maxDownedCount;
        else if (playerNumber == 2)
            return player2DownedCount >= maxDownedCount;
        return false;
    }
    
    public int GetPlayerDownedCount(int playerNumber)
    {
        if (playerNumber == 1)
            return player1DownedCount;
        else if (playerNumber == 2)
            return player2DownedCount;
        return 0;
    }
    
    // Utility methods from GameManager
    public int GetDownedPlayerCount()
    {
        return downedPlayerCount;
    }
    
    public int GetTotalPlayerCount()
    {
        return players.Count;
    }
    
    public bool AreAllPlayersDowned()
    {
        return downedPlayerCount >= players.Count && players.Count > 0;
    }
    
    // Make LevelManager a singleton for easy access
    public static LevelManager Instance { get; private set; }
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
}
