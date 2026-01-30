using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MapGen : MonoBehaviour
{
    [Header("Room Settings")]
    [SerializeField] private GameObject[] roomPrefabs;

    [Header("Corridor Settings")]
    [SerializeField] private GameObject corridorPrefab;
    [SerializeField] private float minCorridorSpacing = 2f;
    [SerializeField] private float maxCorridorSpacing = 4f;
    [SerializeField] private float roomTraversalCost = 0f;

    [Header("Dungeon Expansion")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private int maxRooms = 25;
    [SerializeField] private int maxAttemptsPerOpening = 10;
    [SerializeField] private GameObject wallPrefab;

    [Header("Extra Connections (Loops)")]
    [Range(0f, 1f)]
    [SerializeField] private float extraConnectionChancePerOpening = 0.15f;
    [SerializeField] private int maxExtraConnections = 12;
    [SerializeField] private float maxExtraConnectionDistance = 3.5f;

    private readonly HashSet<RoomGen> generatedRooms = new();
    private readonly List<Bounds> roomBounds = new();
    private readonly List<(RoomOpening A, RoomOpening B)> treeOpenings = new();
    private readonly List<(RoomOpening A, RoomOpening B)> extraOpenings = new();

    private void Start()
    {
        if (!generateOnStart)
        {
            return;
        }

        GenerateDungeon();
    }

    private bool IsValidOpening(RoomOpening opening) => opening != null && !opening.IsConnected;
    
    private bool IsValidEdge((RoomOpening A, RoomOpening B) edge) => edge.A != null && edge.B != null;
    
    private RoomGen GetRoomFromOpening(RoomOpening opening) => opening?.GetComponentInParent<RoomGen>();

    private void GenerateDungeon()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        generatedRooms.Clear();
        roomBounds.Clear();
        treeOpenings.Clear();
        extraOpenings.Clear();

        if (roomPrefabs == null || roomPrefabs.Length == 0)
        {
            return;
        }

        GameObject startPrefab = roomPrefabs[Random.Range(0, roomPrefabs.Length)];
        if (startPrefab == null)
        {
            return;
        }

        RoomGen startRoom = SpawnRoom(startPrefab, transform.position);
        if (startRoom == null)
        {
            return;
        }

        Debug.Log($"Start room spawned. Generated rooms: {generatedRooms.Count}");

        Queue<RoomOpening> pendingOpenings = new();
        foreach (RoomOpening opening in GetOrCreateOpenings(startRoom))
        {
            if (IsValidOpening(opening))
            {
                pendingOpenings.Enqueue(opening);
            }
        }

        while (pendingOpenings.Count > 0 && generatedRooms.Count < maxRooms)
        {
            RoomOpening opening = pendingOpenings.Dequeue();
            if (!IsValidOpening(opening))
            {
                continue;
            }

            Debug.Log($"Processing opening {opening.FacingDirection}. Current rooms: {generatedRooms.Count}/{maxRooms}");

            // Decide whether to branch out with a corridor (50% chance)
            if (Random.value < 0.5f)
            {
                if (TryCreateCorridorAndRoom(opening, out RoomGen newRoom, out RoomOpening newRoomOpening))
                {
                    opening.MarkConnected(newRoomOpening);
                    newRoomOpening.MarkConnected(opening);
                    treeOpenings.Add((opening, newRoomOpening));

                    foreach (RoomOpening next in GetOrCreateOpenings(newRoom))
                    {
                        if (IsValidOpening(next))
                        {
                            pendingOpenings.Enqueue(next);
                        }
                    }
                }
                else
                {
                    opening.Seal(wallPrefab, transform);
                }
            }
            else
            {
                opening.Seal(wallPrefab, transform);
            }
        }

        NameAndConnectMainPath();
    }

    private void NameAndConnectMainPath()
    {
        if (generatedRooms.Count < 2)
        {
            return;
        }

        Dictionary<RoomGen, List<(RoomGen Neighbor, float Weight)>> adjacency = BuildWeightedAdjacency(treeOpenings);
        RoomGen any = null;
        foreach (RoomGen r in generatedRooms)
        {
            if (r != null)
            {
                any = r;
                break;
            }
        }

        if (any == null)
        {
            return;
        }

        // Find longest path (tree diameter) for main path identification
        RoomGen first = FindFurthestRoomByTravelDistance(any, adjacency, out _);
        RoomGen last = FindFurthestRoomByTravelDistance(first, adjacency, out Dictionary<RoomGen, RoomGen> parentFromFirst);

        if (first != null)
        {
            first.gameObject.name = "FirstRoom";
        }

        if (last != null)
        {
            last.gameObject.name = "LastRoom";
        }

        AddExtraConnections();

        // Create corridors for extra connections only (main path corridors already created)
        foreach ((RoomOpening A, RoomOpening B) edge in extraOpenings)
        {
            if (IsValidEdge(edge))
            {
                CreateCorridor(edge.A.transform.position, edge.B.transform.position);
            }
        }

        // Check for and destroy rooms that are fully skewered by corridors
        DestroySkeweredRooms();
    }

    private Dictionary<RoomGen, List<(RoomGen Neighbor, float Weight)>> BuildWeightedAdjacency(List<(RoomOpening A, RoomOpening B)> edges)
    {
        Dictionary<RoomGen, List<(RoomGen Neighbor, float Weight)>> adjacency = new();

        foreach (RoomGen room in generatedRooms.Where(r => r != null))
        {
            adjacency[room] = new List<(RoomGen Neighbor, float Weight)>();
        }

        foreach ((RoomOpening A, RoomOpening B) edge in edges)
        {
            if (!IsValidEdge(edge)) continue;

            RoomGen ra = GetRoomFromOpening(edge.A);
            RoomGen rb = GetRoomFromOpening(edge.B);
            if (ra == null || rb == null) continue;

            Vector3 delta = edge.B.transform.position - edge.A.transform.position;
            delta.y = 0f;
            float weight = delta.magnitude + Mathf.Max(0f, roomTraversalCost);

            adjacency[ra].Add((rb, weight));
            adjacency[rb].Add((ra, weight));
        }

        return adjacency;
    }

    private void AddExtraConnections()
    {
        if (maxExtraConnections <= 0 || extraConnectionChancePerOpening <= 0f)
        {
            return;
        }

        float maxDistSq = maxExtraConnectionDistance * maxExtraConnectionDistance;
        int added = 0;

        List<RoomOpening> candidates = new();
        foreach (RoomGen room in generatedRooms)
        {
            if (room == null)
            {
                continue;
            }

            foreach (RoomOpening o in GetOrCreateOpenings(room))
            {
                if (o != null && !o.IsConnected)
                {
                    candidates.Add(o);
                }
            }
        }

        // Attempt to connect openings that face each other and are close enough.
        for (int i = 0; i < candidates.Count && added < maxExtraConnections; i++)
        {
            RoomOpening a = candidates[i];
            if (a == null || a.IsConnected)
            {
                continue;
            }

            if (Random.value > extraConnectionChancePerOpening)
            {
                continue;
            }

            RoomOpening best = null;
            float bestSq = float.PositiveInfinity;
            RoomOpening.Direction desired = GetOpposite(a.FacingDirection);

            for (int j = 0; j < candidates.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                RoomOpening b = candidates[j];
                if (b == null || b.IsConnected || b.FacingDirection != desired)
                {
                    continue;
                }

                // Don't connect openings within the same room.
                if (a.GetComponentInParent<RoomGen>() == b.GetComponentInParent<RoomGen>())
                {
                    continue;
                }

                Vector3 delta = b.transform.position - a.transform.position;
                delta.y = 0f;
                float dSq = delta.sqrMagnitude;
                if (dSq > maxDistSq)
                {
                    continue;
                }

                // Facing check: openings should point toward each other.
                Vector3 af = a.transform.forward;
                af.y = 0f;
                Vector3 bf = b.transform.forward;
                bf.y = 0f;
                af.Normalize();
                bf.Normalize();

                if (Vector3.Dot(af, -bf) < 0.9f)
                {
                    continue;
                }

                Vector3 dir = delta.normalized;
                if (Vector3.Dot(af, dir) < 0.7f)
                {
                    continue;
                }

                if (dSq < bestSq)
                {
                    bestSq = dSq;
                    best = b;
                }
            }

            if (best == null)
            {
                continue;
            }

            a.MarkConnected(best);
            best.MarkConnected(a);
            extraOpenings.Add((a, best));
            added++;
        }
    }

    private static RoomGen FindFurthestRoomByTravelDistance(RoomGen start, Dictionary<RoomGen, List<(RoomGen Neighbor, float Weight)>> adjacency, out Dictionary<RoomGen, RoomGen> parent)
    {
        parent = new Dictionary<RoomGen, RoomGen>();
        if (start == null)
        {
            return null;
        }

        // Dijkstra-like traversal (O(n^2) selection) - fine for small room counts
        Dictionary<RoomGen, float> dist = new();
        HashSet<RoomGen> visited = new();

        foreach (RoomGen node in adjacency.Keys)
        {
            dist[node] = float.PositiveInfinity;
        }

        dist[start] = 0f;
        parent[start] = null;

        while (visited.Count < adjacency.Count)
        {
            RoomGen cur = null;
            float best = float.PositiveInfinity;
            foreach (KeyValuePair<RoomGen, float> kv in dist)
            {
                if (visited.Contains(kv.Key))
                {
                    continue;
                }

                if (kv.Value < best)
                {
                    best = kv.Value;
                    cur = kv.Key;
                }
            }

            if (cur == null || float.IsPositiveInfinity(best))
            {
                break;
            }

            visited.Add(cur);

            if (!adjacency.TryGetValue(cur, out List<(RoomGen Neighbor, float Weight)> neighbors) || neighbors == null)
            {
                continue;
            }

            for (int i = 0; i < neighbors.Count; i++)
            {
                (RoomGen nxt, float w) = neighbors[i];
                if (nxt == null || visited.Contains(nxt))
                {
                    continue;
                }

                float alt = dist[cur] + Mathf.Max(0f, w);
                if (!dist.TryGetValue(nxt, out float old) || alt < old)
                {
                    dist[nxt] = alt;
                    parent[nxt] = cur;
                }
            }
        }

        RoomGen furthest = start;
        float furthestDist = 0f;
        foreach (KeyValuePair<RoomGen, float> kv in dist)
        {
            if (float.IsPositiveInfinity(kv.Value))
            {
                continue;
            }

            if (kv.Value > furthestDist)
            {
                furthestDist = kv.Value;
                furthest = kv.Key;
            }
        }

        return furthest;
    }

    private static IEnumerable<(RoomGen U, RoomGen V)> EnumeratePathEdges(RoomGen start, RoomGen end, Dictionary<RoomGen, RoomGen> parentFromStart)
    {
        if (start == null || end == null)
        {
            yield break;
        }

        RoomGen cur = end;
        while (cur != null && cur != start)
        {
            if (!parentFromStart.TryGetValue(cur, out RoomGen p) || p == null)
            {
                yield break;
            }

            yield return (p, cur);
            cur = p;
        }
    }

    private RoomGen SpawnRoom(GameObject prefab, Vector3 position)
    {
        GameObject instance = Instantiate(prefab, position, Quaternion.identity, transform);
        RoomGen roomGen = instance.GetComponent<RoomGen>();
        if (roomGen == null)
        {
            roomGen = instance.AddComponent<RoomGen>();
        }

        Bounds bounds = CalculateRoomBounds(instance);

        generatedRooms.Add(roomGen);
        roomBounds.Add(bounds);

        return roomGen;
    }

    private bool TryCreateCorridorAndRoom(RoomOpening fromOpening, out RoomGen newRoom, out RoomOpening newRoomOpening)
    {
        newRoom = null;
        newRoomOpening = null;

        if (roomPrefabs == null || roomPrefabs.Length == 0)
        {
            return false;
        }

        // Try different corridor lengths with adjustment logic
        for (int attempt = 0; attempt < maxAttemptsPerOpening; attempt++)
        {
            float corridorLength = Random.Range(minCorridorSpacing, maxCorridorSpacing);
            
            // Try to place a room with the current corridor length
            if (TryPlaceRoomWithCorridorAdjustment(fromOpening, corridorLength, out newRoom, out newRoomOpening))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryPlaceRoomWithCorridorAdjustment(RoomOpening fromOpening, float initialCorridorLength, out RoomGen newRoom, out RoomOpening newRoomOpening)
    {
        newRoom = null;
        newRoomOpening = null;

        float currentLength = initialCorridorLength;
        float lengthAdjustmentStep = 0.5f;
        int maxAdjustments = 10;

        for (int adjustment = 0; adjustment < maxAdjustments; adjustment++)
        {
            Vector3 corridorEnd = fromOpening.transform.position + fromOpening.transform.forward * currentLength;

            // Try to place a room at the end of the corridor
            if (TryPlaceRoomAtPosition(corridorEnd, fromOpening, out newRoom, out newRoomOpening))
            {
                // Create the corridor with the adjusted length
                CreateCorridor(fromOpening.transform.position, corridorEnd);
                return true;
            }

            // If room placement failed, adjust corridor length
            if (adjustment % 2 == 0)
            {
                // Try shortening the corridor
                currentLength -= lengthAdjustmentStep;
                if (currentLength < minCorridorSpacing)
                {
                    currentLength = minCorridorSpacing;
                }
            }
            else
            {
                // Try lengthening the corridor
                currentLength += lengthAdjustmentStep;
                if (currentLength > maxCorridorSpacing * 2f)
                {
                    currentLength = maxCorridorSpacing * 2f;
                }
            }
        }

        return false;
    }

    private bool TryPlaceRoomAtPosition(Vector3 position, RoomOpening fromOpening, out RoomGen newRoom, out RoomOpening newRoomOpening)
    {
        newRoom = null;
        newRoomOpening = null;

        if (roomPrefabs == null || roomPrefabs.Length == 0)
        {
            return false;
        }

        for (int attempt = 0; attempt < maxAttemptsPerOpening; attempt++)
        {
            GameObject prefab = roomPrefabs[Random.Range(0, roomPrefabs.Length)];
            if (prefab == null)
            {
                continue;
            }

            GameObject instance = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
            RoomGen roomGen = instance.GetComponent<RoomGen>();
            if (roomGen == null)
            {
                roomGen = instance.AddComponent<RoomGen>();
            }

            List<RoomOpening> openings = GetOrCreateOpenings(roomGen);
            RoomOpening opposite = null;
            RoomOpening.Direction desired = GetOpposite(fromOpening.FacingDirection);
            for (int i = 0; i < openings.Count; i++)
            {
                if (openings[i] != null && openings[i].FacingDirection == desired)
                {
                    opposite = openings[i];
                    break;
                }
            }

            if (opposite == null)
            {
                Destroy(instance);
                continue;
            }

            Vector3 localOpposite = instance.transform.InverseTransformPoint(opposite.transform.position);
            instance.transform.position = position - instance.transform.TransformVector(localOpposite);

            Physics.SyncTransforms();

            Bounds candidateBounds = CalculateRoomBounds(instance);

            if (IsRoomClipping(candidateBounds, instance))
            {
                Destroy(instance);
                continue;
            }

            generatedRooms.Add(roomGen);
            roomBounds.Add(candidateBounds);

            newRoom = roomGen;
            newRoomOpening = opposite;
            return true;
        }

        return false;
    }

    private bool IsRoomClipping(Bounds candidateBounds, GameObject roomInstance)
    {
        // Check against existing rooms
        for (int i = 0; i < roomBounds.Count; i++)
        {
            Bounds bufferedExisting = roomBounds[i];
            bufferedExisting.Expand(0.1f);
            
            if (bufferedExisting.Intersects(candidateBounds))
            {
                return true;
            }
        }

        // Check against existing corridors
        foreach (Transform child in transform)
        {
            if (child.name.Contains("Corridor") || child.gameObject.name.Contains("corridor"))
            {
                Bounds corridorBounds = CalculateRoomBounds(child.gameObject);
                Bounds bufferedCorridor = corridorBounds;
                bufferedCorridor.Expand(0.1f);
                
                if (bufferedCorridor.Intersects(candidateBounds))
                {
                    return true;
                }
            }
        }

        return false;
    }
    private static RoomOpening.Direction GetOpposite(RoomOpening.Direction dir)
    {
        return dir switch
        {
            RoomOpening.Direction.North => RoomOpening.Direction.South,
            RoomOpening.Direction.East => RoomOpening.Direction.West,
            RoomOpening.Direction.South => RoomOpening.Direction.North,
            _ => RoomOpening.Direction.East,
        };
    }

    private List<RoomOpening> GetOrCreateOpenings(RoomGen room)
    {
        List<RoomOpening> openings = new(room.GetComponentsInChildren<RoomOpening>());
        if (openings.Count > 0)
        {
            return openings;
        }

        // Use world bounds extents converted to room-local space
        Bounds worldBounds = CalculateRoomBounds(room.gameObject);
        Vector3 localExtents = room.transform.InverseTransformVector(worldBounds.extents);
        Vector3 extents = new Vector3(Mathf.Abs(localExtents.x), Mathf.Abs(localExtents.y), Mathf.Abs(localExtents.z));

        openings.Add(CreateOpening(room.transform, RoomOpening.Direction.North, new Vector3(0f, 0f, extents.z), Quaternion.LookRotation(Vector3.forward)));
        openings.Add(CreateOpening(room.transform, RoomOpening.Direction.East, new Vector3(extents.x, 0f, 0f), Quaternion.LookRotation(Vector3.right)));
        openings.Add(CreateOpening(room.transform, RoomOpening.Direction.South, new Vector3(0f, 0f, -extents.z), Quaternion.LookRotation(Vector3.back)));
        openings.Add(CreateOpening(room.transform, RoomOpening.Direction.West, new Vector3(-extents.x, 0f, 0f), Quaternion.LookRotation(Vector3.left)));
        return openings;
    }

    private static RoomOpening CreateOpening(Transform roomTransform, RoomOpening.Direction direction, Vector3 localPos, Quaternion localRot)
    {
        GameObject go = new GameObject(direction.ToString());
        go.transform.SetParent(roomTransform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;

        RoomOpening opening = go.AddComponent<RoomOpening>();
        typeof(RoomOpening).GetField("direction", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(opening, direction);
        return opening;
    }

    private static Bounds CalculateRoomBounds(GameObject room)
    {
        // Prefer renderer bounds: they update immediately when transforms move
        Renderer[] renderers = room.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        // Fall back to collider bounds if no renderers
        BoxCollider boxCollider = room.GetComponentInChildren<BoxCollider>();
        if (boxCollider != null)
        {
            Bounds worldBounds = boxCollider.bounds;
            return worldBounds;
        }

        Bounds fallback = new Bounds(room.transform.position, Vector3.one);
        return fallback;
    }

    private bool IsOverlappingExistingRoom(Bounds candidate)
    {
        // Add buffer to prevent rooms from getting too close to each other
        Bounds bufferedCandidate = candidate;
        bufferedCandidate.Expand(0.5f); // Add 0.5 unit buffer around candidate room
        
        for (int i = 0; i < roomBounds.Count; i++)
        {
            Bounds bufferedExisting = roomBounds[i];
            bufferedExisting.Expand(0.5f); // Add 0.5 unit buffer around existing room
            
            if (bufferedExisting.Intersects(bufferedCandidate))
            {
                return true;
            }
        }

        return false;
    }

    private void CreateCorridor(Vector3 start, Vector3 end)
    {
        if (corridorPrefab == null)
        {
            return;
        }

        Vector3 direction = end - start;
        direction.y = 0f;

        if (direction == Vector3.zero)
        {
            return;
        }

        float length = direction.magnitude;
        Vector3 midPoint = start + direction * 0.5f;
        Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        GameObject corridor = Instantiate(corridorPrefab, midPoint, rotation, transform);
        Vector3 scale = corridor.transform.localScale;
        scale.z = length;
        corridor.transform.localScale = scale;
    }

    private void DestroySkeweredRooms()
    {
        List<RoomGen> roomsToDestroy = new();
        List<GameObject> corridors = new();

        // Get all corridor objects
        foreach (Transform child in transform)
        {
            if (child.name.Contains("Corridor") || child.gameObject.name.Contains("corridor"))
            {
                corridors.Add(child.gameObject);
            }
        }

        // Check each room against all corridors
        foreach (RoomGen room in generatedRooms.Where(r => r != null))
        {
            if (room == null || room.gameObject == null)
                continue;

            Bounds roomBounds = CalculateRoomBounds(room.gameObject);
            
            foreach (GameObject corridor in corridors)
            {
                if (corridor == null)
                    continue;

                if (IsRoomFullySkewered(roomBounds, corridor))
                {
                    roomsToDestroy.Add(room);
                    break;
                }
            }
        }

        // Destroy skewered rooms and clean up references
        foreach (RoomGen room in roomsToDestroy)
        {
            if (room != null && room.gameObject != null)
            {
                Debug.Log($"Destroying skewered room: {room.gameObject.name}");
                
                // Remove from collections
                generatedRooms.Remove(room);
                
                // Find and remove corresponding bounds
                int boundsIndex = -1;
                for (int i = 0; i < roomBounds.Count; i++)
                {
                    Bounds bounds = roomBounds[i];
                    Vector3 roomCenter = room.gameObject.transform.position;
                    if (Vector3.Distance(bounds.center, roomCenter) < 0.1f)
                    {
                        boundsIndex = i;
                        break;
                    }
                }
                if (boundsIndex >= 0)
                {
                    roomBounds.RemoveAt(boundsIndex);
                }

                // Remove any connected openings from treeOpenings and extraOpenings
                RemoveOpeningsForRoom(room, treeOpenings);
                RemoveOpeningsForRoom(room, extraOpenings);

                Destroy(room.gameObject);
            }
        }
    }

    private bool IsRoomFullySkewered(Bounds roomBounds, GameObject corridor)
    {
        if (corridor == null)
            return false;

        // Get corridor bounds
        Bounds corridorBounds = CalculateRoomBounds(corridor);
        
        // Check if corridor intersects room bounds
        if (!roomBounds.Intersects(corridorBounds))
            return false;

        // Check if corridor passes completely through the room
        // This happens when the corridor extends beyond both opposite faces of the room
        Vector3 roomSize = roomBounds.size;
        Vector3 corridorSize = corridorBounds.size;
        
        // Check X-axis penetration
        if (corridorSize.z > roomSize.x * 0.8f) // Corridor is long enough to potentially skewer
        {
            float corridorCenterX = corridorBounds.center.x;
            float roomCenterX = roomBounds.center.x;
            
            // Check if corridor passes through room's X dimension
            if (Mathf.Abs(corridorCenterX - roomCenterX) < roomSize.x * 0.3f)
            {
                // Check if corridor extends beyond both sides of room in Z direction
                float corridorMinZ = corridorBounds.min.z;
                float corridorMaxZ = corridorBounds.max.z;
                float roomMinZ = roomBounds.min.z;
                float roomMaxZ = roomBounds.max.z;
                
                if (corridorMinZ < roomMinZ && corridorMaxZ > roomMaxZ)
                {
                    return true;
                }
            }
        }
        
        // Check Z-axis penetration
        if (corridorSize.z > roomSize.z * 0.8f) // Corridor is long enough to potentially skewer
        {
            float corridorCenterZ = corridorBounds.center.z;
            float roomCenterZ = roomBounds.center.z;
            
            // Check if corridor passes through room's Z dimension
            if (Mathf.Abs(corridorCenterZ - roomCenterZ) < roomSize.z * 0.3f)
            {
                // Check if corridor extends beyond both sides of room in X direction
                float corridorMinX = corridorBounds.min.x;
                float corridorMaxX = corridorBounds.max.x;
                float roomMinX = roomBounds.min.x;
                float roomMaxX = roomBounds.max.x;
                
                if (corridorMinX < roomMinX && corridorMaxX > roomMaxX)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void RemoveOpeningsForRoom(RoomGen room, List<(RoomOpening A, RoomOpening B)> openingsList)
    {
        for (int i = openingsList.Count - 1; i >= 0; i--)
        {
            (RoomOpening A, RoomOpening B) edge = openingsList[i];
            RoomGen roomA = GetRoomFromOpening(edge.A);
            RoomGen roomB = GetRoomFromOpening(edge.B);
            
            if (roomA == room || roomB == room)
            {
                openingsList.RemoveAt(i);
            }
        }
    }
}
