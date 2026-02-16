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

    [Tooltip("Number of objects to spawn when the room is initialized")]
    [SerializeField] private int spawnCount = 20;

    [Tooltip("Size of the area (centered on this object) to spawn objects within, on the XZ plane")]
    [SerializeField] private Vector3 spawnAreaSize = new Vector3(10f, 0f, 10f);

    [Tooltip("If true, objects will be spawned automatically in Start()")] 
    [SerializeField] private bool spawnOnStart = true;

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnObjects();
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
