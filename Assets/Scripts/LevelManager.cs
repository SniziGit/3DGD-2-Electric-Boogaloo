using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [Header("Game State")]
    public bool isGameOver = false;
    public bool isPaused = false;
    
    [Header("Timer")]
    public float maxTime = 480f; // 8 minutes default
    private float currentTime;
    
    [Header("Game Conditions")]
    public bool playerWon = false;
    public bool outOfTime = false;
    public bool playersDead = false;
    
    [Header("Game Progress")]
    private CollectableManager collectableManager;
    public bool reachedWarpPad = false;
    
    private int player1DownedCount = 0;
    private int player2DownedCount = 0;
    private const int maxDownedCount = 3; // Players die after being downed 3 times
    
    private void Start()
    {
        currentTime = maxTime;
        collectableManager = CollectableManager.Instance;
    }
    
    private void Update()
    {
        if (!isGameOver && !isPaused)
        {
            currentTime -= Time.deltaTime;
            
            if (currentTime <= 0f)
            {
                currentTime = 0f;
                outOfTime = true;
                isGameOver = true;
                return;
            }
            
            // Check win condition: all collectibles collected and reached warp pad
            if (collectableManager != null && collectableManager.GetCurrentCollected() >= collectableManager.GetTotalRequired() && reachedWarpPad)
            {
                playerWon = true;
                isGameOver = true;
                return;
            }
            
            // Check lose condition: both players downed
            if (playersDead)
            {
                isGameOver = true;
                return;
            }
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
        isGameOver = true;
        playerWon = win;
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
    
    // Player downed state management
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
}
