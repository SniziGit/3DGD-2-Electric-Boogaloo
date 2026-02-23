using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    
    private List<PlayerHealth> players = new List<PlayerHealth>();
    private int downedPlayerCount = 0;

    public int roomComplexity;
    public float allocatedTime;
    public int collectableQuantity;

    void Awake()
    {
        // DISABLED - LevelManager now handles all game state management
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
            Destroy(gameObject);
    }
    
    void Start()
    {
        // Find all players with "Player" tag
        GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
        
        foreach (GameObject playerObj in playerObjects)
        {
            PlayerHealth playerHealth = playerObj.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                players.Add(playerHealth);
            }
        }
        DifficultySetting(1); // Default to Easy difficulty

        Debug.Log($"Found {players.Count} players in the game");
    }

    public void DifficultySetting(int difficultyIndex)
    {
        if(difficultyIndex == 1) // Easy
        {
            roomComplexity = 2;
            allocatedTime = 480f; // 8 minutes
            collectableQuantity = 2;
        }
        else if(difficultyIndex == 2) // Medium
        {
            roomComplexity = 3;
            allocatedTime = 360f; // 6 minutes
            collectableQuantity = 5;
        }
        else if(difficultyIndex == 3) // Hard
        {
            roomComplexity = 4;
            allocatedTime = 240f; // 2 minutes
            collectableQuantity = 10;
        }
    }


    
    public void CheckPlayerDowned(PlayerHealth downedPlayer)
    {
        if (!players.Contains(downedPlayer))
        {
            // Add player if not already tracked
            players.Add(downedPlayer);
        }
        
        // Count how many players are downed
        downedPlayerCount = 0;
        foreach (PlayerHealth player in players)
        {
            if (player.IsDowned())
            {
                downedPlayerCount++;
            }
        }
        
        Debug.Log($"Player downed. Total downed players: {downedPlayerCount}/{players.Count}");
        
        // Check if all players are downed
        if (downedPlayerCount >= players.Count && players.Count > 0)
        {
            GameOver();
        }
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
    
    private void GameOver()
    {
        Debug.Log("GAME OVER - All players are downed!");
        // LevelManager now handles all game over logic
    }
    
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
}
