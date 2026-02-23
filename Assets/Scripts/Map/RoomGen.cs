using UnityEngine;

/// <summary>
/// Handles the generation and management of a single terrain chunk
/// </summary>
[RequireComponent(typeof(Transform))]
public class RoomGen : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Prefabs that can be spawned in this room")]
    [SerializeField] private GameObject[] spawnPrefabs;
    [SerializeField] private GameObject[] spawnEnemy;

    [Tooltip("Number of objects to spawn when the room is initialized")]
    [SerializeField] private int spawnCount = 20;
    [SerializeField] private int spawnEnemyCount = 10;

    [Tooltip("Number of patrol points to spawn for enemies")]
    [SerializeField] private int patrolPointCount = 5;

    [Tooltip("Size of the area (centered on this object) to spawn objects within, on the XZ plane")]
    [SerializeField] private Vector3 spawnAreaSize = new Vector3(10f, 0f, 10f);

    public Vector3 GetSpawnAreaSize()
    {
        return spawnAreaSize;
    }

    [Tooltip("If true, objects will be spawned automatically in Start()")] 
    [SerializeField] private bool spawnOnStart = true;
    
    
    private void OnEnable()
    {
        // Spawning will now be manually triggered after room naming is complete
    }

    /// <summary>
    /// Manually initializes spawning after room naming is complete
    /// </summary>
    public void InitializeSpawning()
    {
        // Register this room with the collectable manager
        if (CollectableManager.Instance != null)
        {
            CollectableManager.Instance.RegisterRoom(this);
        }
        
        if (spawnOnStart)
        {
            SpawnObjects();
            SpawnCrystals();
            SpawnPatrolPoints();
            if (this.gameObject.name != "First Room")
            {
                SpawnEnemies();
            }
        }
    }
    
    /// <summary>
    /// Spawns objects randomly within the defined room area, as children of this transform
    /// </summary>
    public void SpawnObjects()
    {
        if (spawnPrefabs == null || spawnPrefabs.Length == 0 || spawnCount <= 0)
        {
            return;
        }
        for (int i = 0; i < spawnCount; i++)
        {
            GameObject prefab = spawnPrefabs[Random.Range(0, spawnPrefabs.Length)];
            if (prefab == null)
            {
                continue;
            }
            // Random position within a box centered on this transform, on XZ plane
            float halfX = spawnAreaSize.x * 0.5f;
            float halfZ = spawnAreaSize.z * 0.5f;
            Vector3 localPos = new Vector3(
                Random.Range(-halfX, halfX),
                0f,
                Random.Range(-halfZ, halfZ)
            );
            Vector3 worldPos = transform.TransformPoint(localPos);
            Quaternion rotation = Quaternion.identity;
            Instantiate(prefab, worldPos, rotation, transform);
        }
    }
    public void SpawnEnemies()
    {
        if (spawnEnemy == null || spawnEnemy.Length == 0 || spawnEnemyCount <= 0)
        {
            return;
        }

        for (int i = 0; i < spawnEnemyCount; i++)
        {
            GameObject prefab = spawnEnemy[Random.Range(0, spawnEnemy.Length)];
            if (prefab == null)
            {
                continue;
            }

            // Random position within a box centered on this transform, on XZ plane
            float halfX = spawnAreaSize.x * 0.5f;
            float halfZ = spawnAreaSize.z * 0.5f;

            Vector3 localPos = new Vector3(
                Random.Range(-halfX, halfX),
                0f,
                Random.Range(-halfZ, halfZ)
            );

            Vector3 worldPos = transform.TransformPoint(localPos);
            Quaternion rotation = Quaternion.identity;

            Instantiate(prefab, worldPos, rotation, transform);
        }
    }
    
    /// <summary>
    /// Spawns patrol points for enemies to follow
    /// </summary>
    public void SpawnPatrolPoints()
    {
        GameObject patrolParent = new GameObject("PatrolPoint");
        patrolParent.tag = "PatrolPoint";
        patrolParent.transform.SetParent(transform);
        
        for (int i = 0; i < patrolPointCount; i++)
        {
            GameObject point = new GameObject($"PatrolPoint_{i}");
            point.transform.SetParent(patrolParent.transform);
            
            Vector3 randomPos = GetRandomSpawnPosition();
            // Place on ground level
            randomPos.y = transform.position.y;
            point.transform.position = randomPos;
        }
    }
    
    public void SetDifficulty(int distanceFromLastRoom)
    {
        // Only apply difficulty scaling to the Last Room (distance = 0)
        // All other rooms keep their default spawn count
        if (distanceFromLastRoom == 0)
        {
            // Last Room gets maximum difficulty
            spawnEnemyCount = 10;
            Debug.Log($"[RoomGen] Set difficulty: Last Room (distance=0) - max difficulty spawnCount={spawnEnemyCount}");
        }
        else
        {
            // All other rooms keep their default spawn count
            // spawnEnemyCount remains at its default value (10)
            Debug.Log($"[RoomGen] Set difficulty: Regular room (distance={distanceFromLastRoom}) - default spawnCount={spawnEnemyCount}");
        }
    }
    
    /// <summary>
    /// Spawns crystals in this room
    /// Note: Spawning is now handled centrally by CollectableManager after MapGen is disabled
    /// </summary>
    public void SpawnCrystals()
    {
        // Spawning is now handled by CollectableManager.SpawnAllCollectables()
        // This method is kept for compatibility but does nothing
    }
    
    /// <summary>
    /// Gets a random position within the spawn area
    /// </summary>
    private Vector3 GetRandomSpawnPosition()
    {
        Vector3 randomPos = transform.position;
        randomPos.x += Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f);
        randomPos.z += Random.Range(-spawnAreaSize.z / 2f, spawnAreaSize.z / 2f);
        
        // Add some height to spawn above ground
        randomPos.y = transform.position.y + 2f;
        
        return randomPos;
    }


#if UNITY_EDITOR
    /// <summary>
    /// Draws gizmos in the Unity editor for visualization
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawWireCube(
            transform.position,
            new Vector3(spawnAreaSize.x, 0.1f, spawnAreaSize.z)
        );
    }
    #endif
}
