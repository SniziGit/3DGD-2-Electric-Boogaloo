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
        // Use a coroutine to wait for the scene to fully load
        StartCoroutine(SpawnPlayerWhenReady());
    }

    private System.Collections.IEnumerator SpawnPlayerWhenReady()
    {
        // Wait a few frames to ensure all objects are initialized
        yield return null;
        yield return null;
        
        SpawnPlayerInFirstRoom();
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

        // Find the First Room by name
        GameObject firstRoom = GameObject.Find("First Room");
        
        if (firstRoom == null)
        {
            Debug.LogError("[PlayerSpawning] Could not find room named 'First Room' in the scene!");
            return;
        }

        // Get the RoomGen component to access spawn area information
        RoomGen roomGen = firstRoom.GetComponent<RoomGen>();
        if (roomGen != null)
        {
            // Spawn player at the center of the room's spawn area
            Vector3 spawnPosition = firstRoom.transform.position;
            spawnPosition.y += spawnHeight; // Spawn slightly above ground
            
            spawnedPlayer = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);

            Debug.Log($"[PlayerSpawning] Player spawned in 'First Room' at position {spawnPosition}");
        }
        else
        {
            Debug.LogWarning("[PlayerSpawning] 'First Room' found but no RoomGen component attached. Spawning at room position.");
            
            // Fallback: spawn at the room's position
            Vector3 spawnPosition = firstRoom.transform.position;
            spawnPosition.y += spawnHeight;
            
            spawnedPlayer = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            
            Debug.Log($"[PlayerSpawning] Player spawned at fallback position {spawnPosition}");
        }
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
