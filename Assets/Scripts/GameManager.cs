using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    
    private List<PlayerHealth> players = new List<PlayerHealth>();
    private int downedPlayerCount = 0;
    
    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
            Instance = this;
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
        
        Debug.Log($"Found {players.Count} players in the game");
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
        
        // Force all players to die
        foreach (PlayerHealth player in players)
        {
            player.ForceDeath();
        }
        
        // You could add game over UI here
        // For example: UIManager.Instance.ShowGameOver();
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
