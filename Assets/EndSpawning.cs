using UnityEngine;
using System.Collections;
using System.Linq;

public class EndSpawning : MonoBehaviour
{
    [Header("End Settings")]
    [Tooltip("Warp Pad prefab to spawn")]
    [SerializeField] private GameObject warpPadPrefab;
    
    [Tooltip("Height above ground to spawn the warp pad")]
    [SerializeField] private float spawnHeight = 1f;
    
    [Tooltip("Delay before spawning warp pad after map generation")]
    [SerializeField] private float spawnDelay = 0f;

    private GameObject spawnedWarpPad;
    
    void Start()
    {
        MapGen.OnMapGenerationComplete += OnMapGenerationComplete;
        
        // Start fallback timer in case event doesn't fire
        StartCoroutine(FallbackSpawnAfterDelay());
    }
    
    private IEnumerator FallbackSpawnAfterDelay()
    {
        yield return new WaitForSeconds(0.1f);
        
        // Only spawn if not already spawned
        if (spawnedWarpPad == null)
        {
            SpawnWarpPadInLastRoom();
        }
    }
    
    private void OnMapGenerationComplete()
    {
        StartCoroutine(SpawnWarpPadWithDelay());
        MapGen.OnMapGenerationComplete -= OnMapGenerationComplete;
    }
    
    private IEnumerator SpawnWarpPadWithDelay()
    {
        if (spawnDelay > 0)
        {
            yield return new WaitForSeconds(spawnDelay);
        }
        
        SpawnWarpPadInLastRoom();
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
        if (warpPadPrefab == null || spawnedWarpPad != null)
        {
            return false;
        }

        GameObject lastRoom = GameObject.Find("Last Room");
        if (lastRoom == null)
        {
            return false;
        }

        Vector3 spawnPosition = lastRoom.transform.position;
        spawnPosition.y += spawnHeight;
        
        spawnedWarpPad = Instantiate(warpPadPrefab, spawnPosition, Quaternion.identity);
        return spawnedWarpPad != null;
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
