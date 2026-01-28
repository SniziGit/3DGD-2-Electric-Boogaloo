using System.Collections.Generic;
using UnityEngine;

public class MapGen : MonoBehaviour
{
    [Header("Room Settings")]
    [Tooltip("Prefabs for different room types")]
    [SerializeField] private GameObject[] roomPrefabs;
    [Tooltip("Number of rooms to generate in the map")]
    [SerializeField] private int roomCount = 10;
    [Tooltip("Size of each room on the XZ plane (assumed square for simplicity)")]
    [SerializeField] private float roomSize = 10f;

    [Header("Map Bounds")]
    [Tooltip("Overall size of the map on the XZ plane. Rooms will be placed within this area, centered on this object.")]
    [SerializeField] private Vector2 mapSize = new Vector2(100f, 100f);

    [Header("Placement Settings")]
    [Tooltip("Maximum attempts to reposition a room before giving up on that room.")]
    [SerializeField] private int maxPlacementAttemptsPerRoom = 10;

    [Header("Corridor Settings")]
    [Tooltip("Prefab used for straight corridor segments.")]
    [SerializeField] private GameObject corridorPrefab;

    // Tracks all generated rooms (for fast membership checks)
    private readonly HashSet<RoomGen> generatedRooms = new();

    // Ordered list so we can connect rooms in generation order
    private readonly List<RoomGen> roomOrder = new();

    // Cached bounds for overlap checks
    private readonly List<Bounds> roomBounds = new();

    private struct CorridorSegment
    {
        public Vector3 Start;
        public Vector3 End;
    }

    private readonly List<CorridorSegment> corridorSegments = new();

    // -------------------------------------------------------------------------
    // Entry point: generate rooms then connect them with corridors
    // -------------------------------------------------------------------------
    private void Start()
    {
        GenerateRooms();
        ConnectRoomsWithCorridors();
    }

    // -------------------------------------------------------------------------
    // Room generation: place non-overlapping rooms within bounds, with limited
    // placement attempts per room. Tracks rooms in a HashSet and ordered list.
    // -------------------------------------------------------------------------
    private void GenerateRooms()
    {
        if (roomPrefabs == null || roomPrefabs.Length == 0 || roomCount <= 0)
        {
            return;
        }

        // Map bounds (centered on this object)
        float halfMapX = mapSize.x * 0.5f;
        float halfMapZ = mapSize.y * 0.5f;
        Vector3 roomSizeVector = new Vector3(roomSize, 0.1f, roomSize);

        for (int i = 0; i < roomCount; i++)
        {
            GameObject prefab = roomPrefabs[Random.Range(0, roomPrefabs.Length)];
            if (prefab == null)
            {
                continue;
            }

            bool placed = false;

            // Try up to maxPlacementAttemptsPerRoom times to find a non-overlapping spot
            for (int attempt = 0; attempt < maxPlacementAttemptsPerRoom; attempt++)
            {
                float x = Random.Range(-halfMapX, halfMapX);
                float z = Random.Range(-halfMapZ, halfMapZ);
                Vector3 position = new Vector3(x, 0f, z) + transform.position;

                Bounds candidateBounds = new Bounds(position, roomSizeVector);

                if (IsOverlappingExistingRoom(candidateBounds))
                {
                    continue;
                }

                // Instantiate room and ensure it has a RoomGen component
                GameObject instance = Instantiate(prefab, position, Quaternion.identity, transform);
                RoomGen roomGen = instance.GetComponent<RoomGen>();

                if (roomGen == null)
                {
                    roomGen = instance.AddComponent<RoomGen>();
                }

                // Track the new room
                generatedRooms.Add(roomGen);
                roomOrder.Add(roomGen);
                roomBounds.Add(candidateBounds);

                placed = true;
                break;
            }

            // If not placed after all attempts, we simply skip this room
            if (!placed)
            {
                // Could log or handle this case if desired
            }
        }

        // -------------------------------------------------------------------------
        // Name the first and last rooms for easy identification
        // -------------------------------------------------------------------------
        if (roomOrder.Count > 0)
        {
            roomOrder[0].gameObject.name = "First";
        }
        if (roomOrder.Count > 1)
        {
            roomOrder[^1].gameObject.name = "Last";
        }
    }

    // -------------------------------------------------------------------------
    // Overlap check: test candidate bounds against all existing room bounds
    // -------------------------------------------------------------------------
    private bool IsOverlappingExistingRoom(Bounds candidate)
    {
        for (int i = 0; i < roomBounds.Count; i++)
        {
            if (roomBounds[i].Intersects(candidate))
            {
                return true;
            }
        }

        return false;
    }

    // -------------------------------------------------------------------------
    // Corridor connection: link rooms in generation order with straight or L-shaped paths
    // -------------------------------------------------------------------------
    private void ConnectRoomsWithCorridors()
    {
        if (corridorPrefab == null)
        {
            return;
        }

        if (roomOrder.Count < 2)
        {
            return;
        }

        // Connect each room to the next in the ordered list
        for (int i = 0; i < roomOrder.Count - 1; i++)
        {
            RoomGen from = roomOrder[i];
            RoomGen to = roomOrder[i + 1];

            if (from == null || to == null)
            {
                continue;
            }

            Vector3 start = from.transform.position;
            Vector3 end = to.transform.position;

            // If already aligned on X or Z, one straight corridor is enough
            if (Mathf.Approximately(start.x, end.x) || Mathf.Approximately(start.z, end.z))
            {
                CreateCorridorSegment(start, end);
            }
            else
            {
                // L-shaped: move in X first, then Z (90-degree turn on XZ plane)
                Vector3 corner = new Vector3(end.x, start.y, start.z);
                CreateCorridorSegment(start, corner);
                CreateCorridorSegment(corner, end);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Create a single straight corridor segment from start to end,
    // skipping redundant segments that are very close and parallel to existing ones
    // -------------------------------------------------------------------------
    private void CreateCorridorSegment(Vector3 start, Vector3 end)
    {
        Vector3 direction = end - start;

        if (direction == Vector3.zero)
        {
            return;
        }

        // Work on XZ plane only
        direction.y = 0f;

        float length = direction.magnitude;
        Vector3 midPoint = start + direction * 0.5f;

        // Skip creating corridors that are very close and parallel to an existing one
        if (IsRedundantCorridor(start, end))
        {
            return;
        }

        // Rotate corridor to face from start to end, then scale along its forward (Z) axis
        Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        GameObject corridor = Instantiate(corridorPrefab, midPoint, rotation, transform);

        Vector3 scale = corridor.transform.localScale;
        scale.z = length;
        corridor.transform.localScale = scale;

        // Record this segment for redundancy checks
        corridorSegments.Add(new CorridorSegment { Start = start, End = end });
    }

    // -------------------------------------------------------------------------
    // Redundancy check: if a new segment is very close, parallel, and overlapping
    // with an existing segment on the same axis, consider it redundant
    // -------------------------------------------------------------------------
    private bool IsRedundantCorridor(Vector3 start, Vector3 end)
    {
        Vector3 dir = end - start;
        dir.y = 0f;

        if (dir == Vector3.zero)
        {
            return true;
        }

        bool isHorizontal = Mathf.Abs(dir.x) > Mathf.Abs(dir.z);
        float redundancyDistance = roomSize * 0.01f; // tolerance for “very close”

        foreach (var seg in corridorSegments)
        {
            Vector3 existingDir = seg.End - seg.Start;
            existingDir.y = 0f;

            if (existingDir == Vector3.zero)
            {
                continue;
            }

            bool existingHorizontal = Mathf.Abs(existingDir.x) > Mathf.Abs(existingDir.z);

            // Only consider segments along the same main axis
            if (existingHorizontal != isHorizontal)
            {
                continue;
            }

            if (isHorizontal)
            {
                // Both horizontal: check if Z is very close and X ranges overlap
                if (Mathf.Abs(start.z - seg.Start.z) > redundancyDistance)
                {
                    continue;
                }

                float minX1 = Mathf.Min(start.x, end.x);
                float maxX1 = Mathf.Max(start.x, end.x);
                float minX2 = Mathf.Min(seg.Start.x, seg.End.x);
                float maxX2 = Mathf.Max(seg.Start.x, seg.End.x);

                bool overlapX = maxX1 >= minX2 && maxX2 >= minX1;
                if (overlapX)
                {
                    return true;
                }
            }
            else
            {
                // Both vertical: check if X is very close and Z ranges overlap
                if (Mathf.Abs(start.x - seg.Start.x) > redundancyDistance)
                {
                    continue;
                }

                float minZ1 = Mathf.Min(start.z, end.z);
                float maxZ1 = Mathf.Max(start.z, end.z);
                float minZ2 = Mathf.Min(seg.Start.z, seg.End.z);
                float maxZ2 = Mathf.Max(seg.Start.z, seg.End.z);

                bool overlapZ = maxZ1 >= minZ2 && maxZ2 >= minZ1;
                if (overlapZ)
                {
                    return true;
                }
            }
        }

        return false;
    }
}