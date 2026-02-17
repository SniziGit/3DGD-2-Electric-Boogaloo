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

    [Tooltip("Size of the area (centered on this object) to spawn objects within, on the XZ plane")]
    [SerializeField] private Vector3 spawnAreaSize = new Vector3(10f, 0f, 10f);

    [Tooltip("If true, objects will be spawned automatically in Start()")] 
    [SerializeField] private bool spawnOnStart = true;
    
    
    private void OnEnable()
    {
        if (spawnOnStart)
        {
            SpawnObjects();
            SpawnEnemies();
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
    
    
    public void SetDifficulty(int distanceFromLastRoom)
    {
        // More enemies the closer to the Last Room (inverted difficulty)
        // distanceFromLastRoom: 0 = Last Room (hardest), higher numbers = easier
        int maxDistance = 10; // Maximum expected distance
        float difficultyRatio = 1f - (float)distanceFromLastRoom / maxDistance;
        difficultyRatio = Mathf.Clamp01(difficultyRatio);
        
        // Spawn count: 5 (easiest) to 10 (hardest)
        spawnEnemyCount = Mathf.RoundToInt(5 + difficultyRatio * 5);

        Debug.Log($"[RoomGen] Set difficulty: distance={distanceFromLastRoom}, ratio={difficultyRatio:F2}, spawnCount={spawnCount}");
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
