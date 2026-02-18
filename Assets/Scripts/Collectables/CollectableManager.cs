using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class CollectableManager : MonoBehaviour
{
    [Header("Collectable Settings")]
    [SerializeField] private int totalRequiredCollectables = 10;
    [SerializeField] private string collectableName = "Crystal";
    [SerializeField] private GameObject crystalPrefab;
    
    [Header("UI Display")]
    [SerializeField] private TextMeshProUGUI collectableText;
    
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
        UpdateCollectableUI();
    }
    
    public void CollectCollectable(GameObject player)
    {
        if (gameCompleted)
            return;
        
        currentCollected++;
        
        Debug.Log($"{player.name} collected a {collectableName}! Progress: {currentCollected}/{totalRequiredCollectables}");
        
        UpdateCollectableUI();
        
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
        
        // Randomly decide if this room should get a crystal
        if (Random.value < 0.5f && crystalsToSpawn > 0)
        {
            crystalsToSpawn--;
            return true;
        }
        
        return false;
    }
    
    public GameObject GetCrystalPrefab()
    {
        return crystalPrefab;
    }
    
    public string GetCollectionText()
    {
        return $"{collectableName}s: {currentCollected}/{totalRequiredCollectables}";
    }
    
    public string GetDetailedCollectionText()
    {
        return $"Collected {currentCollected} out of {totalRequiredCollectables} {collectableName}s ({GetRemaining()} remaining)";
    }
    
    void CompleteGame()
    {
        gameCompleted = true;
        
        Debug.Log("Game Completed! All collectables gathered!");
        
        UpdateCollectableUI();
        
        // UI and completion effects are now handled by InteractionPreview.cs
    }
    
    void UpdateCollectableUI()
    {
        if (collectableText != null)
        {
            collectableText.text = GetCollectionText();
        }
    }
    
    // For testing purposes
    [ContextMenu("Reset Progress")]
    public void ResetProgress()
    {
        currentCollected = 0;
        gameCompleted = false;
        crystalsToSpawn = totalRequiredCollectables;
        UpdateCollectableUI();
    }
    
    [ContextMenu("Add Test Collectable")]
    public void AddTestCollectable()
    {
        CollectCollectable(gameObject);
    }
}
