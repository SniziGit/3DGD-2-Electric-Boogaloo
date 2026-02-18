using UnityEngine;
using System.Collections;

public class EndSpawning : MonoBehaviour
{
    [Header("End Settings")]
    [Tooltip("Warp Pad prefab to spawn")]
    [SerializeField] private GameObject warpPadPrefab;
    
    [Tooltip("Height above ground to spawn the warp pad")]
    [SerializeField] private float spawnHeight = 1f;
    
    [Tooltip("How long to wait before attempting to spawn (to allow map generation to complete)")]
    [SerializeField] private float spawnDelay = 1f;

    private GameObject spawnedWarpPad;

    void Start()
    {
        StartCoroutine(DelayedSpawn());
    }

    private IEnumerator DelayedSpawn()
    {
        // Wait for map generation to complete
        yield return new WaitForSeconds(spawnDelay);
        
        // Try to spawn, with retries if room not found yet
        int maxRetries = 5;
        float retryInterval = 0.5f;
        
        for (int i = 0; i < maxRetries; i++)
        {
            if (SpawnWarpPadInLastRoom())
            {
                yield break; // Success, exit coroutine
            }
            
            Debug.LogWarning($"[EndSpawning] Last Room not found, retrying in {retryInterval} seconds... (attempt {i + 1}/{maxRetries})");
            yield return new WaitForSeconds(retryInterval);
        }
        
        Debug.LogError("[EndSpawning] Failed to find Last Room after all retries. Map generation may have failed.");
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
            return false; // Room not found, but don't log error here (let retry logic handle it)
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
