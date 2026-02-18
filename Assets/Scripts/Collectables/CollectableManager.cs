using UnityEngine;
using System.Collections.Generic;

public class CollectableManager : MonoBehaviour
{
    [Header("Collectable Settings")]
    [SerializeField] private int totalRequiredCollectables = 10;
    [SerializeField] private string collectableName = "Crystal";
    [SerializeField] private GameObject crystalPrefab;
    
    private static CollectableManager instance;
    private int currentCollected = 0;
    private bool gameCompleted = false;
    private int crystalsToSpawn = 0;
    private List<RoomGen> availableRooms = new List<RoomGen>();
    
    public static CollectableManager Instance
    {
        get { return instance; }
    }
    
    public int GetCurrentCollected()
    {
        return currentCollected;
    }
    
    public int GetTotalRequired()
    {
        return totalRequiredCollectables;
    }
    
    public int GetRemaining()
    {
        return totalRequiredCollectables - currentCollected;
    }
    
    public bool IsGameCompleted()
    {
        return gameCompleted;
    }
    
    void Awake()
    {
        // Singleton pattern
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        crystalsToSpawn = totalRequiredCollectables;
    }
    
    public void CollectCollectable(GameObject player)
    {
        if (gameCompleted)
            return;
        
        currentCollected++;
        
        Debug.Log($"{player.name} collected a {collectableName}! Progress: {currentCollected}/{totalRequiredCollectables}");
        
        // Check if game is completed
        if (currentCollected >= totalRequiredCollectables)
        {
            CompleteGame();
        }
    }
    
    public void RegisterRoom(RoomGen room)
    {
        if (!availableRooms.Contains(room))
        {
            availableRooms.Add(room);
        }
    }
    
    public bool ShouldSpawnCrystalInRoom(RoomGen room)
    {
        if (crystalsToSpawn <= 0 || !availableRooms.Contains(room))
            return false;
        
        // Calculate remaining rooms that haven't been checked yet
        int remainingRooms = availableRooms.Count - availableRooms.IndexOf(room);
        
        // Ensure we spawn exactly the required number of crystals
        // If we have more crystals to spawn than remaining rooms, spawn in this room
        if (crystalsToSpawn >= remainingRooms)
        {
            crystalsToSpawn--;
            Debug.Log($"[CollectableManager] Spawning crystal (forced). Remaining to spawn: {crystalsToSpawn}");
            return true;
        }
        
        // Otherwise, use probability to distribute remaining crystals among remaining rooms
        float spawnProbability = (float)crystalsToSpawn / remainingRooms;
        if (Random.value < spawnProbability)
        {
            crystalsToSpawn--;
            Debug.Log($"[CollectableManager] Spawning crystal (probabilistic). Probability: {spawnProbability:F2}, Remaining to spawn: {crystalsToSpawn}");
            return true;
        }
        
        return false;
    }
    
    public GameObject GetCrystalPrefab()
    {
        return crystalPrefab;
    }
    
    void CompleteGame()
    {
        gameCompleted = true;
        
        Debug.Log("Game Completed! All collectables gathered!");
        
        // UI and completion effects are now handled by InteractionPreview.cs
    }
    
    // For testing purposes
    [ContextMenu("Reset Progress")]
    public void ResetProgress()
    {
        currentCollected = 0;
        gameCompleted = false;
        crystalsToSpawn = totalRequiredCollectables;
    }
    
    [ContextMenu("Add Test Collectable")]
    public void AddTestCollectable()
    {
        CollectCollectable(gameObject);
    }
}
