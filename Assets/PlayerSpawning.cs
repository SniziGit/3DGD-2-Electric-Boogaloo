using UnityEngine;

public class PlayerSpawning : MonoBehaviour
{
    [Header("Player Settings")]
    [Tooltip("Player prefab to spawn")]
    [SerializeField] private GameObject playerPrefab;
    
    [Tooltip("Height above ground to spawn the player")]
    [SerializeField] private float spawnHeight = 1f;

    [Header("Split Screen Settings")]
    [Tooltip("Main camera for split screen setup")]
    [SerializeField] private Camera mainCamera;

    private GameObject[] spawnedPlayers = new GameObject[2];
    private Camera[] playerCameras = new Camera[2];

    void Start()
    {
        // Use a coroutine to wait for the scene to fully load
        StartCoroutine(SpawnPlayerWhenReady());
    }

    private System.Collections.IEnumerator SpawnPlayerWhenReady()
    {
        // Wait a few frames to ensure all objects are initialized
        yield return null;
        yield return null;
        
        SpawnPlayersInFirstRoom();
    }

    /// <summary>
    /// Finds the room named "First Room" and spawns 2 players there with split-screen cameras
    /// </summary>
    private void SpawnPlayersInFirstRoom()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerSpawning] Player prefab is not assigned!");
            return;
        }

        // Find the First Room by name
        GameObject firstRoom = GameObject.Find("First Room");
        
        if (firstRoom == null)
        {
            Debug.LogError("[PlayerSpawning] Could not find room named 'First Room' in the scene!");
            return;
        }

        // Get the RoomGen component to access spawn area information
        RoomGen roomGen = firstRoom.GetComponent<RoomGen>();
        Vector3 baseSpawnPosition = firstRoom.transform.position;
        baseSpawnPosition.y += spawnHeight; // Spawn slightly above ground
        
        // Spawn 2 players at slightly different positions
        for (int i = 0; i < 2; i++)
        {
            Vector3 spawnPosition = baseSpawnPosition;
            spawnPosition.x += (i == 0) ? -1f : 1f; // Offset players horizontally
            
            spawnedPlayers[i] = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            
            // Setup camera for each player
            SetupPlayerCamera(i, spawnPosition);
            
            Debug.Log($"[PlayerSpawning] Player {i + 1} spawned in 'First Room' at position {spawnPosition}");
        }
        
        // Setup split-screen cameras
        SetupSplitScreen();
    }
    
    /// <summary>
    /// Sets up camera for each player using existing camera on player prefab
    /// </summary>
    private void SetupPlayerCamera(int playerIndex, Vector3 spawnPosition)
    {
        // Get the existing camera from the player prefab
        Camera playerCamera = spawnedPlayers[playerIndex].GetComponentInChildren<Camera>();
        
        if (playerCamera != null)
        {
            playerCameras[playerIndex] = playerCamera;
            Debug.Log($"[PlayerSpawning] Found existing camera for Player {playerIndex + 1}");
        }
        else
        {
            Debug.LogError($"[PlayerSpawning] No camera found on Player {playerIndex + 1} prefab!");
        }
    }
    
    /// <summary>
    /// Sets up split-screen view
    /// </summary>
    private void SetupSplitScreen()
    {
        if (playerCameras[0] == null || playerCameras[1] == null)
        {
            Debug.LogError("[PlayerSpawning] Player cameras not found!");
            return;
        }
        
        // Disable main camera if it exists
        if (mainCamera != null)
        {
            mainCamera.enabled = false;
        }
        
        // Setup viewport rectangles for split screen
        playerCameras[0].rect = new Rect(0f, 0f, 0.5f, 1f);  // Left half
        playerCameras[1].rect = new Rect(0.5f, 0f, 0.5f, 1f); // Right half
        
        Debug.Log("[PlayerSpawning] Split-screen cameras configured");
    }

    /// <summary>
    /// Gets the spawned player instances
    /// </summary>
    /// <returns>Array of spawned player GameObjects, or null if no players were spawned</returns>
    public GameObject[] GetSpawnedPlayers()
    {
        return spawnedPlayers;
    }
    
    /// <summary>
    /// Gets a specific spawned player instance
    /// </summary>
    /// <param name="playerIndex">Index of the player (0 or 1)</param>
    /// <returns>The spawned player GameObject, or null if not found</returns>
    public GameObject GetSpawnedPlayer(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < spawnedPlayers.Length)
        {
            return spawnedPlayers[playerIndex];
        }
        return null;
    }

    /// <summary>
    /// Respawns both players in the First Room
    /// </summary>
    public void RespawnPlayers()
    {
        for (int i = 0; i < spawnedPlayers.Length; i++)
        {
            if (spawnedPlayers[i] != null)
            {
                Destroy(spawnedPlayers[i]);
            }
        }
        
        SpawnPlayersInFirstRoom();
    }
    
    /// <summary>
    /// Legacy method for backward compatibility
    /// </summary>
    public void RespawnPlayer()
    {
        RespawnPlayers();
    }
}
