using UnityEngine;

public class PlayerSpawning : MonoBehaviour
{
    [Header("Player Settings")]
    [Tooltip("Player prefab to spawn")]
    [SerializeField] private GameObject playerPrefab;
    
    [Tooltip("Height above ground to spawn the player")]
    [SerializeField] private float spawnHeight = 1f;

    private GameObject spawnedPlayer;

    void Start()
    {
        Debug.Log("[PlayerSpawning] Start() called, subscribing to MapGen completion event");
        
        // Check if MapGen is already disabled (generation already complete)
        MapGen mapGen = FindObjectOfType<MapGen>();
        if (mapGen != null && !mapGen.enabled)
        {
            Debug.Log("[PlayerSpawning] MapGen is already disabled, spawning player immediately");
            SpawnPlayerInFirstRoom();
            return;
        }
        
        // Subscribe to MapGen completion event
        MapGen.OnMapGenerationComplete += OnMapGenerationComplete;
        Debug.Log("[PlayerSpawning] Subscribed to OnMapGenerationComplete event");
    }
    
    private void OnMapGenerationComplete()
    {
        Debug.Log("[PlayerSpawning] MapGen generation complete event received, spawning player");
        SpawnPlayerInFirstRoom();
        
        // Unsubscribe after use to prevent memory leaks
        MapGen.OnMapGenerationComplete -= OnMapGenerationComplete;
    }
    
    private void OnDestroy()
    {
        // Clean up event subscription
        MapGen.OnMapGenerationComplete -= OnMapGenerationComplete;
    }

    /// <summary>
    /// Finds the room named "First Room" and spawns the player there
    /// </summary>
    private void SpawnPlayerInFirstRoom()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerSpawning] Player prefab is not assigned!");
            return;
        }

        Debug.Log("[PlayerSpawning] Looking for 'First Room'...");
        
        // Find the First Room by name
        GameObject firstRoom = GameObject.Find("First Room");
        
        if (firstRoom == null)
        {
            Debug.LogError("[PlayerSpawning] Could not find room named 'First Room' in scene!");
            
            // List all room objects for debugging
            GameObject[] allRooms = GameObject.FindGameObjectsWithTag("Untagged");
            Debug.Log($"[PlayerSpawning] Found {allRooms.Length} untagged objects:");
            for (int i = 0; i < Mathf.Min(allRooms.Length, 10); i++)
            {
                Debug.Log($"[PlayerSpawning] Object {i}: {allRooms[i].name}");
            }
            return;
        }

        Debug.Log($"[PlayerSpawning] Found 'First Room' at position {firstRoom.transform.position}");

        // Calculate spawn position
        Vector3 spawnPosition = firstRoom.transform.position;
        spawnPosition.y += spawnHeight; // Spawn slightly above ground
        
        // Spawn player
        spawnedPlayer = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        
        Debug.Log($"[PlayerSpawning] Player spawned in 'First Room' at position {spawnPosition}");
    }

    /// <summary>
    /// Gets the spawned player instance
    /// </summary>
    /// <returns>The spawned player GameObject, or null if no player was spawned</returns>
    public GameObject GetSpawnedPlayer()
    {
        return spawnedPlayer;
    }

    /// <summary>
    /// Respawns the player in the First Room
    /// </summary>
    public void RespawnPlayer()
    {
        if (spawnedPlayer != null)
        {
            Destroy(spawnedPlayer);
        }
        
        SpawnPlayerInFirstRoom();
    }
}
