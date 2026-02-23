using UnityEngine;
using System.Collections;

public class EndSpawning : MonoBehaviour
{
    [Header("End Settings")]
    [Tooltip("Warp Pad prefab to spawn")]
    [SerializeField] private GameObject warpPadPrefab;
    
    [Tooltip("Height above ground to spawn the warp pad")]
    [SerializeField] private float spawnHeight = 1f;

    private GameObject spawnedWarpPad;

    void Start()
    {
        // Subscribe to MapGen completion event
        MapGen.OnMapGenerationComplete += OnMapGenerationComplete;
    }
    
    private void OnMapGenerationComplete()
    {
        Debug.Log("[EndSpawning] MapGen generation complete event received, spawning warp pad");
        SpawnWarpPadInLastRoom();
        
        // Unsubscribe after use to prevent memory leaks
        MapGen.OnMapGenerationComplete -= OnMapGenerationComplete;
    }
    
    private void OnDestroy()
    {
        // Clean up event subscription
        MapGen.OnMapGenerationComplete -= OnMapGenerationComplete;
    }

    /// <summary>
    /// Finds the room named "Last Room" and spawns the warp pad there
    /// </summary>
    /// <returns>true if spawning was successful, false if room was not found</returns>
    private bool SpawnWarpPadInLastRoom()
    {
        if (warpPadPrefab == null)
        {
            Debug.LogError("[EndSpawning] Warp Pad prefab is not assigned!");
            return false;
        }

        // Find the Last Room by name
        GameObject lastRoom = GameObject.Find("Last Room");
        
        if (lastRoom == null)
        {
            Debug.LogError("[EndSpawning] Last Room not found! Map generation may have failed or room naming is incorrect.");
            return false;
        }

        // Calculate spawn position
        Vector3 spawnPosition = lastRoom.transform.position;
        spawnPosition.y += spawnHeight; // Spawn slightly above ground
        
        // Spawn the warp pad
        spawnedWarpPad = Instantiate(warpPadPrefab, spawnPosition, Quaternion.identity);
        
        Debug.Log($"[EndSpawning] Warp Pad spawned in 'Last Room' at position {spawnPosition}");
        return true;
    }

    /// <summary>
    /// Gets the spawned warp pad instance
    /// </summary>
    /// <returns>The spawned warp pad GameObject, or null if no warp pad was spawned</returns>
    public GameObject GetSpawnedWarpPad()
    {
        return spawnedWarpPad;
    }

    /// <summary>
    /// Respawns the warp pad in the Last Room
    /// </summary>
    public void RespawnWarpPad()
    {
        if (spawnedWarpPad != null)
        {
            Destroy(spawnedWarpPad);
        }
        
        SpawnWarpPadInLastRoom();
    }
}
