using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class CollectableManager : MonoBehaviour
{
    [Header("Collectable Settings")]
    int totalRequiredCollectables = 10;
    [SerializeField] private string collectableName = "Crystal";
    [SerializeField] private GameObject crystalPrefab;
    
    [Header("UI Display")]
    [SerializeField] private TextMeshProUGUI collectableText;
    
    private static CollectableManager instance;
    public int currentCollected = 0;
    [SerializeField] public bool allCollected = false;
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
        return allCollected;
    }
    
    void Awake()
    {
        totalRequiredCollectables = GameManager.Instance.collectableQuantity;
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
        
        MapGen mapGen = FindObjectOfType<MapGen>();
        if (mapGen != null && !mapGen.enabled)
        {
            SpawnAllCollectables();
            return;
        }
        
        MapGen.OnMapGenerationComplete += OnMapGenerationComplete;
    }
    
    private void OnMapGenerationComplete()
    {
        SpawnAllCollectables();
        MapGen.OnMapGenerationComplete -= OnMapGenerationComplete;
    }
    
    private void OnDestroy()
    {
        // Clean up event subscription
        MapGen.OnMapGenerationComplete -= OnMapGenerationComplete;
    }
    
    private void SpawnAllCollectables()
    {
        if (availableRooms.Count == 0)
        {
            Debug.LogWarning("[CollectableManager] No rooms registered for spawning!");
            return;
        }
        
        // Shuffle rooms for random distribution
        List<RoomGen> shuffledRooms = new List<RoomGen>(availableRooms);
        System.Random rng = new System.Random();
        for (int i = shuffledRooms.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            RoomGen temp = shuffledRooms[i];
            shuffledRooms[i] = shuffledRooms[j];
            shuffledRooms[j] = temp;
        }
        
        // Distribute collectables among rooms
        int remainingCrystals = crystalsToSpawn;
        int roomIndex = 0;
        
        while (remainingCrystals > 0 && roomIndex < shuffledRooms.Count)
        {
            RoomGen currentRoom = shuffledRooms[roomIndex];
            
            // Calculate how many crystals to place in this room
            int remainingRooms = shuffledRooms.Count - roomIndex;
            int maxCrystalsInThisRoom = Mathf.Min(remainingCrystals, Mathf.CeilToInt((float)remainingCrystals / remainingRooms) + 1);
            int crystalsToPlaceInRoom = Random.Range(1, maxCrystalsInThisRoom + 1);
            
            // Ensure we don't exceed the total required
            crystalsToPlaceInRoom = Mathf.Min(crystalsToPlaceInRoom, remainingCrystals);
            
            Debug.Log($"[CollectableManager] Placing {crystalsToPlaceInRoom} crystals in room {currentRoom.gameObject.name}");
            
            // Spawn the crystals in this room
            for (int i = 0; i < crystalsToPlaceInRoom; i++)
            {
                SpawnCrystalInRoom(currentRoom);
            }
            
            remainingCrystals -= crystalsToPlaceInRoom;
            roomIndex++;
        }
        
        Debug.Log($"[CollectableManager] Spawned {totalRequiredCollectables - remainingCrystals} collectables across {roomIndex} rooms");
    }
    
    public void CollectCollectable(GameObject player)
    {
        if (allCollected)
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
        // This method is deprecated - spawning is now handled centrally by SpawnAllCollectables()
        return false;
    }
    
    public GameObject GetCrystalPrefab()
    {
        return crystalPrefab;
    }
    
    public GameObject SpawnCrystalInRoom(RoomGen room)
    {
        if (room == null)
        {
            Debug.LogWarning("[CollectableManager] Room is null!");
            return null;
        }
        
        GameObject crystalPrefab = GetCrystalPrefab();
        if (crystalPrefab == null)
        {
            Debug.LogWarning("[CollectableManager] Crystal prefab not assigned!");
            return null;
        }
        
        Vector3 randomPosition = GetRandomSpawnPositionInRoom(room);
        GameObject crystal = Instantiate(crystalPrefab, randomPosition, Quaternion.identity);
        crystal.transform.SetParent(room.transform);
        
        Debug.Log($"[CollectableManager] Spawned crystal in {room.gameObject.name}");
        return crystal;
    }
    
    private Vector3 GetRandomSpawnPositionInRoom(RoomGen room)
    {
        Vector3 roomPosition = room.transform.position;
        Vector3 roomSize = room.GetSpawnAreaSize();
        
        Vector3 randomPos = roomPosition;
        randomPos.x += Random.Range(-roomSize.x / 2f, roomSize.x / 2f);
        randomPos.z += Random.Range(-roomSize.z / 2f, roomSize.z / 2f);
        
        // Add some height to spawn above ground (same as original RoomGen method)
        randomPos.y = roomPosition.y + 2f;
        
        return randomPos;
    }
    
    public string GetCollectionText()
    {
        return $"{currentCollected}/{totalRequiredCollectables}";
    }
    
    public string GetDetailedCollectionText()
    {
        return $"Collected {currentCollected} out of {totalRequiredCollectables} {collectableName}s ({GetRemaining()} remaining)";
    }
    
    void CompleteGame()
    {
        allCollected = true;
        
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
        allCollected = false;
        crystalsToSpawn = totalRequiredCollectables;
        UpdateCollectableUI();
    }
    
    [ContextMenu("Add Test Collectable")]
    public void AddTestCollectable()
    {
        CollectCollectable(gameObject);
    }
}
