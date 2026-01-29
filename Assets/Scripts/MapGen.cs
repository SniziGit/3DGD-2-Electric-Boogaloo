using System.Collections.Generic;
using UnityEngine;

public class MapGen : MonoBehaviour
{
    [Header("Room Settings")]
    [Tooltip("Prefabs for different room types")]
    [SerializeField] private GameObject[] roomPrefabs;

    [Header("Corridor Settings")]
    [Tooltip("Prefab used for straight corridor segments.")]
    [SerializeField] private GameObject corridorPrefab;

    [Tooltip("Distance to leave between two connected room openings (corridor length/gap).")]
    [SerializeField] private float corridorSpacing = 3f;

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

    // Tracks all generated rooms (for fast membership checks)
    private readonly HashSet<RoomGen> generatedRooms = new();

    // Cached bounds for overlap checks
    private readonly List<Bounds> roomBounds = new();

    private readonly List<(RoomOpening A, RoomOpening B)> treeOpenings = new();
    private readonly List<(RoomOpening A, RoomOpening B)> extraOpenings = new();

    // -------------------------------------------------------------------------
    // Entry point: generate rooms then connect them with corridors
    // -------------------------------------------------------------------------
    private void Start()
    {
        if (!generateOnStart)
        {
            return;
        }

        GenerateDungeon();
    }

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
            if (opening != null && !opening.IsConnected)
            {
                pendingOpenings.Enqueue(opening);
                Debug.Log($"Added opening {opening.FacingDirection} to queue");
            }
        }

        Debug.Log($"Starting generation with {pendingOpenings.Count} pending openings");

        while (pendingOpenings.Count > 0 && generatedRooms.Count < maxRooms)
        {
            RoomOpening opening = pendingOpenings.Dequeue();
            if (opening == null || opening.IsConnected)
            {
                continue;
            }

            Debug.Log($"Processing opening {opening.FacingDirection}. Current rooms: {generatedRooms.Count}/{maxRooms}");

            if (!TrySpawnConnectedRoom(opening, out RoomGen newRoom, out RoomOpening newRoomOpening))
            {
                Debug.Log($"Failed to spawn room for opening {opening.FacingDirection} - sealing with wall");
                opening.Seal(wallPrefab, transform);
                continue;
            }

            Debug.Log($"Successfully spawned new room for opening {opening.FacingDirection}");
            opening.MarkConnected(newRoomOpening);
            newRoomOpening.MarkConnected(opening);

            treeOpenings.Add((opening, newRoomOpening));

            foreach (RoomOpening next in GetOrCreateOpenings(newRoom))
            {
                if (next != null && !next.IsConnected)
                {
                    pendingOpenings.Enqueue(next);
                    Debug.Log($"Added new opening {next.FacingDirection} to queue");
                }
            }
        }

        NameAndConnectMainPath();
        Debug.Log($"Generation complete. Total rooms: {generatedRooms.Count}");
    }

    private void NameAndConnectMainPath()
    {
        if (generatedRooms.Count < 2)
        {
            foreach ((RoomOpening A, RoomOpening B) edge in treeOpenings)
            {
                if (edge.A != null && edge.B != null)
                {
                    CreateCorridor(edge.A.transform.position, edge.B.transform.position);
                }
            }

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

        // Longest possible main path in this generated graph (tree diameter):
        // 1) BFS from any node to find one endpoint.
        // 2) BFS from that endpoint to find the opposite endpoint.
        // The parent map from step 2 reconstructs the main path.
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

        HashSet<(RoomGen, RoomGen)> mainPathEdges = new();
        foreach ((RoomGen U, RoomGen V) in EnumeratePathEdges(first, last, parentFromFirst))
        {
            mainPathEdges.Add((U, V));
            mainPathEdges.Add((V, U));
        }

        AddExtraConnections();

        // Instantiate corridors on the main path first.
        foreach ((RoomOpening A, RoomOpening B) edge in treeOpenings)
        {
            if (edge.A == null || edge.B == null)
            {
                continue;
            }

            RoomGen ra = edge.A.GetComponentInParent<RoomGen>();
            RoomGen rb = edge.B.GetComponentInParent<RoomGen>();
            if (ra == null || rb == null)
            {
                continue;
            }

            if (mainPathEdges.Contains((ra, rb)))
            {
                CreateCorridor(edge.A.transform.position, edge.B.transform.position);
            }
        }

        // Then instantiate all remaining tree corridors so everything branches into the main path.
        foreach ((RoomOpening A, RoomOpening B) edge in treeOpenings)
        {
            if (edge.A == null || edge.B == null)
            {
                continue;
            }

            RoomGen ra = edge.A.GetComponentInParent<RoomGen>();
            RoomGen rb = edge.B.GetComponentInParent<RoomGen>();
            if (ra == null || rb == null)
            {
                continue;
            }

            if (!mainPathEdges.Contains((ra, rb)))
            {
                CreateCorridor(edge.A.transform.position, edge.B.transform.position);
            }
        }

        // Finally, instantiate extra loop corridors.
        foreach ((RoomOpening A, RoomOpening B) edge in extraOpenings)
        {
            if (edge.A == null || edge.B == null)
            {
                continue;
            }

            CreateCorridor(edge.A.transform.position, edge.B.transform.position);
        }
    }

    private Dictionary<RoomGen, List<(RoomGen Neighbor, float Weight)>> BuildWeightedAdjacency(List<(RoomOpening A, RoomOpening B)> edges)
    {
        Dictionary<RoomGen, List<(RoomGen Neighbor, float Weight)>> adjacency = new();

        foreach (RoomGen room in generatedRooms)
        {
            if (room == null)
            {
                continue;
            }

            adjacency[room] = new List<(RoomGen Neighbor, float Weight)>();
        }

        foreach ((RoomOpening A, RoomOpening B) edge in edges)
        {
            if (edge.A == null || edge.B == null)
            {
                continue;
            }

            RoomGen ra = edge.A.GetComponentInParent<RoomGen>();
            RoomGen rb = edge.B.GetComponentInParent<RoomGen>();
            if (ra == null || rb == null)
            {
                continue;
            }

            Vector3 delta = edge.B.transform.position - edge.A.transform.position;
            delta.y = 0f;
            float weight = delta.magnitude + Mathf.Max(0f, roomTraversalCost);

            if (!adjacency.TryGetValue(ra, out List<(RoomGen Neighbor, float Weight)> aList))
            {
                aList = new List<(RoomGen Neighbor, float Weight)>();
                adjacency[ra] = aList;
            }

            if (!adjacency.TryGetValue(rb, out List<(RoomGen Neighbor, float Weight)> bList))
            {
                bList = new List<(RoomGen Neighbor, float Weight)>();
                adjacency[rb] = bList;
            }

            aList.Add((rb, weight));
            bList.Add((ra, weight));
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

        // Dijkstra-like traversal (O(n^2) selection) - fine for small room counts.
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

    private bool TrySpawnConnectedRoom(RoomOpening fromOpening, out RoomGen newRoom, out RoomOpening newRoomOpening)
    {
        newRoom = null;
        newRoomOpening = null;

        if (roomPrefabs == null || roomPrefabs.Length == 0)
        {
            return false;
        }

        int attempts = 0;
        while (attempts < maxAttemptsPerOpening)
        {
            attempts++;

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

            float spacing = Mathf.Max(0f, corridorSpacing);
            Vector3 targetWorld = fromOpening.transform.position + fromOpening.transform.forward * spacing;
            Debug.Log($"From opening position: {targetWorld}, Local opposite: {localOpposite}");
            instance.transform.position = targetWorld - instance.transform.TransformVector(localOpposite);
            Debug.Log($"New room positioned at: {instance.transform.position}");

            // Important: Collider.bounds can lag behind Transform changes until the next physics step.
            // Force the physics engine to sync transforms so bounds are correct for overlap checks.
            Physics.SyncTransforms();

            Bounds candidateBounds = CalculateRoomBounds(instance);
            Debug.Log($"Candidate bounds: center={candidateBounds.center}, size={candidateBounds.size}");

            if (IsOverlappingExistingRoom(candidateBounds))
            {
                Debug.Log($"Room overlap detected - destroying instance. Attempt {attempts}/{maxAttemptsPerOpening}");
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

        // Use world bounds extents (accounts for scaling) converted back into room-local space.
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
        // Prefer renderer bounds: they update immediately when transforms move.
        Renderer[] renderers = room.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Debug.Log($"Renderer world bounds: center={bounds.center}, size={bounds.size}");
            return bounds;
        }

        // Fall back to collider bounds if there are no renderers.
        BoxCollider boxCollider = room.GetComponentInChildren<BoxCollider>();
        if (boxCollider != null)
        {
            Bounds worldBounds = boxCollider.bounds;
            Debug.Log($"BoxCollider world bounds: center={worldBounds.center}, size={worldBounds.size}");
            return worldBounds;
        }

        Bounds fallback = new Bounds(room.transform.position, Vector3.one);
        Debug.Log($"Fallback bounds: center={fallback.center}, size={fallback.size}");
        return fallback;
    }

    private bool IsOverlappingExistingRoom(Bounds candidate)
    {
        // Add a larger buffer to prevent rooms that are just touching from being considered overlapping
        Bounds bufferedCandidate = candidate;
        bufferedCandidate.Expand(-0.5f); // Increase buffer to 0.5 units
        
        for (int i = 0; i < roomBounds.Count; i++)
        {
            Bounds bufferedExisting = roomBounds[i];
            bufferedExisting.Expand(-0.5f); // Increase buffer to 0.5 units
            
            Debug.Log($"Checking against existing room {i}: center={bufferedExisting.center}, size={bufferedExisting.size}");
            
            if (bufferedExisting.Intersects(bufferedCandidate))
            {
                Debug.Log($"Overlap detected with room {i}");
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
}
