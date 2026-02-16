using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MapGen : MonoBehaviour
{
    private struct CorridorSegment
    {
        public Vector3 Start;
        public Vector3 End;
        public GameObject CorridorObject;
        public RoomOpening FromOpening;
        public RoomOpening ToOpening;
    }
    [Header("Room Settings")]
    [SerializeField] private GameObject[] roomPrefabs;

    [Header("Corridor Settings")]
    [SerializeField] private GameObject corridorPrefab;
    [SerializeField] private GameObject corridorConnectorPrefab;
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
    private readonly List<CorridorSegment> corridorSegments = new();
    private readonly List<Vector3> intersectionPoints = new();
    
    // Memory optimization: Object pools and caches
    private readonly Dictionary<GameObject, Bounds> boundsCache = new();
    private readonly Queue<GameObject> roomPool = new();
    private readonly Queue<GameObject> corridorPool = new();
    private readonly List<GameObject> objectsToDestroy = new();
    
    // Pre-allocated collections to reduce memory allocations
    private readonly List<RoomOpening> tempOpeningsList = new();
    private readonly List<Vector3> tempVectorList = new();

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
            objectsToDestroy.Add(transform.GetChild(i).gameObject);
        }
        DestroyQueuedObjects();

        ClearCollectionsAndCaches();

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

            // Dynamic branching chance based on current room count
            // Early generation: higher chance to branch, later: more selective
            float branchChance = Mathf.Lerp(0.8f, 0.3f, (float)generatedRooms.Count / maxRooms);
            
            // Decide whether to branch out with a corridor
            if (Random.value < branchChance)
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
                    // Only seal if we have enough rooms already, otherwise give it another chance
                    if (generatedRooms.Count >= 3)
                    {
                        opening.Seal(wallPrefab, transform);
                    }
                    else
                    {
                        // Re-queue for another attempt when we have very few rooms
                        pendingOpenings.Enqueue(opening);
                    }
                }
            }
            else
            {
                // Only seal if we have enough rooms, otherwise be more permissive
                if (generatedRooms.Count >= 3)
                {
                    opening.Seal(wallPrefab, transform);
                }
                else
                {
                    // Re-queue for another attempt when we have very few rooms
                    pendingOpenings.Enqueue(opening);
                }
            }
        }

        NameAndConnectMainPath();
        
        // Final cleanup
        DestroyQueuedObjects();
        System.GC.Collect();
    }
    
    private void ClearCollectionsAndCaches()
    {
        generatedRooms.Clear();
        roomBounds.Clear();
        treeOpenings.Clear();
        extraOpenings.Clear();
        corridorSegments.Clear();
        intersectionPoints.Clear();
        boundsCache.Clear();
        tempOpeningsList.Clear();
        tempVectorList.Clear();
    }
    
    private void DestroyQueuedObjects()
    {
        foreach (GameObject obj in objectsToDestroy)
        {
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }
        objectsToDestroy.Clear();
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
                CreateCorridor(edge.A.transform.position, edge.B.transform.position, edge.A, edge.B);
            }
        }

        // Process corridor intersections and create connectors
        ProcessCorridorIntersections();

        // Create connections for corridors that pass through rooms
        CreateSkeweredRoomConnections();

        // Check for and destroy isolated rooms (rooms with no connections)
        DestroyIsolatedRooms();
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
        GameObject instance = GetPooledRoom(prefab);
        if (instance == null)
        {
            instance = Instantiate(prefab, position, Quaternion.identity, transform);
        }
        else
        {
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.SetParent(transform);
            instance.SetActive(true);
        }
        
        RoomGen roomGen = instance.GetComponent<RoomGen>();
        if (roomGen == null)
        {
            roomGen = instance.AddComponent<RoomGen>();
        }

        Bounds bounds = GetCachedBounds(instance);

        generatedRooms.Add(roomGen);
        roomBounds.Add(bounds);

        return roomGen;
    }
    
    private GameObject GetPooledRoom(GameObject prefab)
    {
        if (roomPool.Count > 0)
        {
            GameObject pooled = roomPool.Dequeue();
            if (pooled != null)
            {
                return pooled;
            }
        }
        return null;
    }
    
    private void ReturnToPool(GameObject obj)
    {
        if (obj == null) return;
        
        obj.SetActive(false);
        obj.transform.SetParent(null);
        
        if (obj.name.Contains("Corridor"))
        {
            corridorPool.Enqueue(obj);
        }
        else
        {
            roomPool.Enqueue(obj);
        }
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
                CreateCorridor(fromOpening.transform.position, corridorEnd, fromOpening, newRoomOpening);
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

            GameObject instance = GetPooledRoom(prefab);
            if (instance == null)
            {
                instance = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
            }
            else
            {
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;
                instance.transform.SetParent(transform);
                instance.SetActive(true);
            }
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
                objectsToDestroy.Add(instance);
                continue;
            }

            Vector3 localOpposite = instance.transform.InverseTransformPoint(opposite.transform.position);
            instance.transform.position = position - instance.transform.TransformVector(localOpposite);

            Physics.SyncTransforms();

            Bounds candidateBounds = CalculateRoomBounds(instance);

            if (IsRoomClipping(candidateBounds, instance))
            {
                objectsToDestroy.Add(instance);
                continue;
            }

            // Check if corridors would be too close and try to reposition if needed
            Vector3 adjustedPosition = TryAdjustPositionForCorridorSpacing(instance.transform.position, candidateBounds, fromOpening);
            if (adjustedPosition != instance.transform.position)
            {
                instance.transform.position = adjustedPosition;
                Physics.SyncTransforms();
                candidateBounds = CalculateRoomBounds(instance);
                
                // Re-check clipping after adjustment
                if (IsRoomClipping(candidateBounds, instance))
                {
                    objectsToDestroy.Add(instance);
                    continue;
                }
            }

            generatedRooms.Add(roomGen);
            roomBounds.Add(candidateBounds);

            newRoom = roomGen;
            newRoomOpening = opposite;
            return true;
        }

        return false;
    }

    private Vector3 TryAdjustPositionForCorridorSpacing(Vector3 roomPosition, Bounds roomBounds, RoomOpening fromOpening)
    {
        float minCorridorDistance = 2f; // Minimum distance between corridors
        float adjustmentStep = 1f;
        int maxAdjustmentAttempts = 8;

        // Simulate where the corridor would be placed
        Vector3 corridorStart = fromOpening.transform.position;
        Vector3 corridorEnd = roomPosition + (roomBounds.center - roomPosition); // Approximate room center
        
        for (int attempt = 0; attempt < maxAdjustmentAttempts; attempt++)
        {
            bool needsAdjustment = false;
            Vector3 bestAdjustment = Vector3.zero;
            float minDistance = float.PositiveInfinity;

            // Check against existing corridors
            foreach (Transform child in transform)
            {
                if (child.name.Contains("Corridor") || child.gameObject.name.Contains("corridor"))
                {
                    Bounds existingCorridorBounds = CalculateRoomBounds(child.gameObject);
                    
                    // Check if our potential corridor would be too close
                    Vector3 closestPoint = ClosestPointOnLineSegment(corridorStart, corridorEnd, existingCorridorBounds.center);
                    float distance = Vector3.Distance(closestPoint, existingCorridorBounds.center);
                    
                    if (distance < minCorridorDistance)
                    {
                        needsAdjustment = true;
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            // Calculate adjustment direction (away from existing corridor)
                            Vector3 adjustmentDir = (closestPoint - existingCorridorBounds.center).normalized;
                            if (adjustmentDir == Vector3.zero)
                            {
                                adjustmentDir = Vector3.right; // Default direction
                            }
                            bestAdjustment = adjustmentDir * adjustmentStep;
                        }
                    }
                }
            }

            if (!needsAdjustment)
            {
                break; // No adjustment needed
            }

            // Apply adjustment
            Vector3 newPosition = roomPosition + bestAdjustment;
            
            // Check if new position would cause room clipping
            Bounds testBounds = roomBounds;
            testBounds.center = newPosition;
            
            if (!WouldRoomClip(testBounds))
            {
                roomPosition = newPosition;
                corridorEnd = roomPosition + (roomBounds.center - roomPosition); // Update corridor end
            }
            else
            {
                // Try different directions if primary adjustment causes clipping
                Vector3[] alternativeDirections = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
                bool foundValidAdjustment = false;
                
                foreach (Vector3 dir in alternativeDirections)
                {
                    Vector3 altPosition = roomPosition + dir * adjustmentStep;
                    Bounds altBounds = roomBounds;
                    altBounds.center = altPosition;
                    
                    if (!WouldRoomClip(altBounds))
                    {
                        roomPosition = altPosition;
                        corridorEnd = roomPosition + (roomBounds.center - roomPosition);
                        foundValidAdjustment = true;
                        break;
                    }
                }
                
                if (!foundValidAdjustment)
                {
                    break; // Can't adjust further without clipping
                }
            }
        }

        return roomPosition;
    }

    private bool WouldRoomClip(Bounds candidateBounds)
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

    private Vector3 ClosestPointOnLineSegment(Vector3 lineStart, Vector3 lineEnd, Vector3 point)
    {
        Vector3 lineDirection = lineEnd - lineStart;
        float lineLength = lineDirection.magnitude;
        lineDirection.Normalize();
        
        Vector3 pointDirection = point - lineStart;
        float projection = Vector3.Dot(pointDirection, lineDirection);
        
        projection = Mathf.Clamp(projection, 0f, lineLength);
        
        return lineStart + lineDirection * projection;
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
        tempOpeningsList.Clear();
        tempOpeningsList.AddRange(room.GetComponentsInChildren<RoomOpening>());
        
        if (tempOpeningsList.Count > 0)
        {
            return new List<RoomOpening>(tempOpeningsList);
        }

        // Use world bounds extents converted to room-local space
        Bounds worldBounds = GetCachedBounds(room.gameObject);
        Vector3 localExtents = room.transform.InverseTransformVector(worldBounds.extents);
        Vector3 extents = new Vector3(Mathf.Abs(localExtents.x), Mathf.Abs(localExtents.y), Mathf.Abs(localExtents.z));

        tempOpeningsList.Add(CreateOpening(room.transform, RoomOpening.Direction.North, new Vector3(0f, 0f, extents.z), Quaternion.LookRotation(Vector3.forward)));
        tempOpeningsList.Add(CreateOpening(room.transform, RoomOpening.Direction.East, new Vector3(extents.x, 0f, 0f), Quaternion.LookRotation(Vector3.right)));
        tempOpeningsList.Add(CreateOpening(room.transform, RoomOpening.Direction.South, new Vector3(0f, 0f, -extents.z), Quaternion.LookRotation(Vector3.back)));
        tempOpeningsList.Add(CreateOpening(room.transform, RoomOpening.Direction.West, new Vector3(-extents.x, 0f, 0f), Quaternion.LookRotation(Vector3.left)));
        
        return new List<RoomOpening>(tempOpeningsList);
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

    private Bounds GetCachedBounds(GameObject room)
    {
        if (boundsCache.TryGetValue(room, out Bounds cached))
        {
            return cached;
        }
        
        Bounds bounds = CalculateRoomBounds(room);
        boundsCache[room] = bounds;
        return bounds;
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

    private void CreateCorridor(Vector3 start, Vector3 end, RoomOpening fromOpening = null, RoomOpening toOpening = null)
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

        // Check if we need an L-shaped corridor (rooms aren't aligned on X or Z axis)
        float xDiff = Mathf.Abs(end.x - start.x);
        float zDiff = Mathf.Abs(end.z - start.z);
        float alignmentThreshold = 0.5f; // Tolerance for "aligned" rooms

        bool needsLShape = xDiff > alignmentThreshold && zDiff > alignmentThreshold;

        if (needsLShape)
        {
            CreateZShapedCorridor(start, end, fromOpening, toOpening);
        }
        else
        {
            CreateStraightCorridor(start, end, fromOpening, toOpening);
        }
    }

    private void CreateStraightCorridor(Vector3 start, Vector3 end, RoomOpening fromOpening = null, RoomOpening toOpening = null)
    {
        Vector3 direction = end - start;
        direction.y = 0f;
        
        float length = direction.magnitude;
        Vector3 midPoint = start + direction * 0.5f;
        Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        GameObject corridor = GetPooledCorridor();
        if (corridor == null)
        {
            corridor = Instantiate(corridorPrefab, midPoint, rotation, transform);
        }
        else
        {
            corridor.transform.position = midPoint;
            corridor.transform.rotation = rotation;
            corridor.transform.SetParent(transform);
            corridor.SetActive(true);
        }
        
        Vector3 scale = corridor.transform.localScale;
        scale.z = length;
        corridor.transform.localScale = scale;

        // Track the corridor segment
        CorridorSegment segment = new CorridorSegment
        {
            Start = start,
            End = end,
            CorridorObject = corridor,
            FromOpening = fromOpening,
            ToOpening = toOpening
        };
        corridorSegments.Add(segment);
    }
    
    private GameObject GetPooledCorridor()
    {
        if (corridorPool.Count > 0)
        {
            GameObject pooled = corridorPool.Dequeue();
            if (pooled != null)
            {
                return pooled;
            }
        }
        return null;
    }

    private void CreateZShapedCorridor(Vector3 start, Vector3 end, RoomOpening fromOpening = null, RoomOpening toOpening = null)
    {
        // Create Z-shaped corridor with two corners to avoid sharing coordinates with either room
        float xDiff = Mathf.Abs(end.x - start.x);
        float zDiff = Mathf.Abs(end.z - start.z);
        
        Vector3 firstCorner, secondCorner;
        
        if (xDiff > zDiff)
        {
            // Primary movement in X direction
            // First corner: move partially in X, keep original Z
            firstCorner = new Vector3(start.x + (end.x - start.x) * 0.5f, start.y, start.z);
            // Second corner: move to final Z, keep intermediate X
            secondCorner = new Vector3(firstCorner.x, start.y, end.z);
        }
        else
        {
            // Primary movement in Z direction
            // First corner: move partially in Z, keep original X
            firstCorner = new Vector3(start.x, start.y, start.z + (end.z - start.z) * 0.5f);
            // Second corner: move to final X, keep intermediate Z
            secondCorner = new Vector3(end.x, start.y, firstCorner.z);
        }

        // Try to adjust corners to avoid room clipping
        Vector3 adjustedFirstCorner = AdjustCornerToAvoidRoomClipping(start, firstCorner);
        Vector3 adjustedSecondCorner = AdjustCornerToAvoidRoomClipping(adjustedFirstCorner, secondCorner);

        // Create three segments for Z-shape
        CreateStraightCorridor(start, adjustedFirstCorner, fromOpening, null);
        CreateStraightCorridor(adjustedFirstCorner, adjustedSecondCorner, null, null);
        CreateStraightCorridor(adjustedSecondCorner, end, null, toOpening);

        Debug.Log($"Created Z-shaped corridor from {start} to {end} with corners at {adjustedFirstCorner} and {adjustedSecondCorner}");
    }

    private Vector3 AdjustCornerToAvoidRoomClipping(Vector3 segmentStart, Vector3 corner)
    {
        float adjustmentRadius = 1f; // How far to check for room clipping
        float adjustmentStep = 0.5f;
        int maxAttempts = 6;

        // Check if the corner segment would clip through any rooms
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            bool hasClipping = false;
            
            // Check the path from segmentStart to corner
            foreach (Bounds roomBound in roomBounds)
            {
                if (DoesLineSegmentIntersectBounds(segmentStart, corner, roomBound))
                {
                    hasClipping = true;
                    break;
                }
            }

            if (!hasClipping)
            {
                break; // No clipping found
            }

            // Try to adjust the corner position
            Vector3[] adjustmentDirections = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            bool foundValidPosition = false;

            foreach (Vector3 dir in adjustmentDirections)
            {
                Vector3 testCorner = corner + dir * adjustmentStep;
                
                // Check if adjusted path would still clip
                bool adjustedPathClips = false;
                foreach (Bounds roomBound in roomBounds)
                {
                    if (DoesLineSegmentIntersectBounds(segmentStart, testCorner, roomBound))
                    {
                        adjustedPathClips = true;
                        break;
                    }
                }

                if (!adjustedPathClips)
                {
                    corner = testCorner;
                    foundValidPosition = true;
                    break;
                }
            }

            if (!foundValidPosition)
            {
                break; // Can't find a valid adjustment
            }
        }

        return corner;
    }

    private bool DoesLineSegmentIntersectBounds(Vector3 start, Vector3 end, Bounds bounds)
    {
        // Simple check: if either endpoint is inside the bounds
        if (bounds.Contains(start) || bounds.Contains(end))
        {
            return true;
        }

        // Check if the line segment intersects the bounds
        Vector3 lineDirection = end - start;
        float lineLength = lineDirection.magnitude;
        
        if (lineLength == 0)
        {
            return false;
        }

        lineDirection.Normalize();

        // Project bounds center onto the line
        Vector3 toBoundsCenter = bounds.center - start;
        float projection = Vector3.Dot(toBoundsCenter, lineDirection);
        
        // Clamp projection to line segment
        projection = Mathf.Clamp(projection, 0f, lineLength);
        
        Vector3 closestPoint = start + lineDirection * projection;
        
        // Check if closest point is within bounds (with some tolerance)
        float tolerance = 0.1f;
        Bounds expandedBounds = bounds;
        expandedBounds.Expand(tolerance);
        
        return expandedBounds.Contains(closestPoint);
    }

    private void ProcessCorridorIntersections()
    {
        if (corridorConnectorPrefab == null || corridorSegments.Count < 2)
        {
            return;
        }

        // Optimize intersection detection with spatial hashing
        Dictionary<Vector2Int, List<CorridorSegment>> spatialHash = new();
        float cellSize = 5f;
        
        // Build spatial hash
        for (int i = 0; i < corridorSegments.Count; i++)
        {
            CorridorSegment segment = corridorSegments[i];
            Vector2Int startCell = GetCell(segment.Start, cellSize);
            Vector2Int endCell = GetCell(segment.End, cellSize);
            
            // Add segment to all cells it passes through
            List<Vector2Int> cells = GetCellsAlongLine(startCell, endCell);
            foreach (Vector2Int cell in cells)
            {
                if (!spatialHash.ContainsKey(cell))
                {
                    spatialHash[cell] = new List<CorridorSegment>();
                }
                spatialHash[cell].Add(segment);
            }
        }

        // Check intersections only within same cells
        HashSet<(int, int)> checkedPairs = new();
        for (int i = 0; i < corridorSegments.Count; i++)
        {
            CorridorSegment segmentA = corridorSegments[i];
            Vector2Int startCell = GetCell(segmentA.Start, cellSize);
            Vector2Int endCell = GetCell(segmentA.End, cellSize);
            List<Vector2Int> cells = GetCellsAlongLine(startCell, endCell);
            
            foreach (Vector2Int cell in cells)
            {
                if (!spatialHash.TryGetValue(cell, out List<CorridorSegment> cellSegments))
                    continue;
                    
                foreach (CorridorSegment segmentB in cellSegments)
                {
                    if (ReferenceEquals(segmentA, segmentB)) continue;
                    
                    int pairKey = segmentA.GetHashCode() ^ segmentB.GetHashCode();
                    if (checkedPairs.Contains((i, corridorSegments.IndexOf(segmentB)))) continue;
                    checkedPairs.Add((i, corridorSegments.IndexOf(segmentB)));
                    
                    // Skip if segments share an opening (they're already connected)
                    if (segmentA.FromOpening == segmentB.FromOpening || 
                        segmentA.FromOpening == segmentB.ToOpening ||
                        segmentA.ToOpening == segmentB.FromOpening || 
                        segmentA.ToOpening == segmentB.ToOpening)
                    {
                        continue;
                    }

                    Vector3 intersectionPoint;
                    if (TryFindIntersection(segmentA, segmentB, out intersectionPoint))
                    {
                        // Check if we already have a connector at this intersection
                        bool existingIntersection = false;
                        foreach (Vector3 existing in intersectionPoints)
                        {
                            if (Vector3.Distance(existing, intersectionPoint) < 0.5f)
                            {
                                existingIntersection = true;
                                break;
                            }
                        }

                        if (!existingIntersection)
                        {
                            intersectionPoints.Add(intersectionPoint);
                            CreateCorridorConnector(intersectionPoint, segmentA, segmentB);
                        }
                    }
                }
            }
        }
    }
    
    private Vector2Int GetCell(Vector3 position, float cellSize)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / cellSize),
            Mathf.FloorToInt(position.z / cellSize)
        );
    }
    
    private List<Vector2Int> GetCellsAlongLine(Vector2Int start, Vector2Int end)
    {
        tempVectorList.Clear();
        
        int dx = Mathf.Abs(end.x - start.x);
        int dy = Mathf.Abs(end.y - start.y);
        int sx = start.x < end.x ? 1 : -1;
        int sy = start.y < end.y ? 1 : -1;
        int err = dx - dy;
        
        int x = start.x;
        int y = start.y;
        
        while (true)
        {
            tempVectorList.Add(new Vector3(x, 0, y));
            
            if (x == end.x && y == end.y) break;
            
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
        }
        
        List<Vector2Int> result = new();
        foreach (Vector3 v in tempVectorList)
        {
            result.Add(new Vector2Int((int)v.x, (int)v.z));
        }
        return result;
    }

    private bool TryFindIntersection(CorridorSegment segmentA, CorridorSegment segmentB, out Vector3 intersectionPoint)
    {
        intersectionPoint = Vector3.zero;

        // Get the 2D lines (ignoring Y axis)
        Vector2 startA = new Vector2(segmentA.Start.x, segmentA.Start.z);
        Vector2 endA = new Vector2(segmentA.End.x, segmentA.End.z);
        Vector2 startB = new Vector2(segmentB.Start.x, segmentB.Start.z);
        Vector2 endB = new Vector2(segmentB.End.x, segmentB.End.z);

        // Calculate intersection using line segment intersection formula
        Vector2 lineA = endA - startA;
        Vector2 lineB = endB - startB;

        float cross = lineA.x * lineB.y - lineA.y * lineB.x;
        
        // Lines are parallel
        if (Mathf.Abs(cross) < 0.001f)
        {
            return false;
        }

        Vector2 diff = startB - startA;
        float t = (diff.x * lineB.y - diff.y * lineB.x) / cross;
        float u = (diff.x * lineA.y - diff.y * lineA.x) / cross;

        // Check if intersection point is within both line segments
        if (t >= 0f && t <= 1f && u >= 0f && u <= 1f)
        {
            Vector2 intersection2D = startA + t * lineA;
            intersectionPoint = new Vector3(intersection2D.x, segmentA.Start.y, intersection2D.y);
            return true;
        }

        return false;
    }

    private void CreateCorridorConnector(Vector3 position, CorridorSegment segmentA, CorridorSegment segmentB)
    {
        GameObject connector = Instantiate(corridorConnectorPrefab, position, Quaternion.identity, transform);
        connector.name = "CorridorConnector";

        Debug.Log($"Created corridor connector at intersection: {position}");

        // Split and reconnect corridor segments to the connector
        SplitAndReconnectCorridor(segmentA, position, connector);
        SplitAndReconnectCorridor(segmentB, position, connector);
    }

    private void SplitAndReconnectCorridor(CorridorSegment segment, Vector3 intersectionPoint, GameObject connector)
    {
        if (segment.CorridorObject == null)
            return;

        // Get connector bounds to calculate proper corridor endpoints
        Bounds connectorBounds = CalculateRoomBounds(connector);
        float connectorRadius = Mathf.Max(connectorBounds.extents.x, connectorBounds.extents.z);

        // Calculate direction from corridor start to intersection
        Vector3 directionToIntersection = (intersectionPoint - segment.Start).normalized;
        directionToIntersection.y = 0f;

        // Calculate direction from intersection to corridor end
        Vector3 directionFromIntersection = (segment.End - intersectionPoint).normalized;
        directionFromIntersection.y = 0f;

        // Calculate adjusted endpoints that stop at connector edges
        Vector3 adjustedIntersectionPoint1 = intersectionPoint - directionToIntersection * connectorRadius;
        Vector3 adjustedIntersectionPoint2 = intersectionPoint + directionFromIntersection * connectorRadius;

        // Destroy the original corridor segment
        ReturnToPool(segment.CorridorObject);

        // Create two new corridor segments that connect to the connector edges
        CreateCorridor(segment.Start, adjustedIntersectionPoint1, segment.FromOpening, null);
        CreateCorridor(adjustedIntersectionPoint2, segment.End, null, segment.ToOpening);

        // Remove the original segment from tracking
        corridorSegments.Remove(segment);

        Debug.Log($"Split corridor at intersection point {intersectionPoint}, adjusted endpoints to avoid clipping");
    }

    private void CreateSkeweredRoomConnections()
    {
        List<GameObject> corridors = new();

        // Get all corridor objects
        foreach (Transform child in transform)
        {
            if (child.name.Contains("Corridor") || child.gameObject.name.Contains("corridor"))
            {
                corridors.Add(child.gameObject);
            }
        }

        // Check each room against all corridors for skewering
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
                    Debug.Log($"Corridor skewers room {room.gameObject.name}, creating connections");
                    CreateSkeweredRoomConnection(room, corridor);
                    break; // Only handle one skewering per room for now
                }
            }
        }
    }

    private void CreateSkeweredRoomConnection(RoomGen room, GameObject corridor)
    {
        Bounds roomBounds = CalculateRoomBounds(room.gameObject);
        Bounds corridorBounds = CalculateRoomBounds(corridor);
        
        // Determine the skewering direction
        Vector3 roomSize = roomBounds.size;
        Vector3 corridorSize = corridorBounds.size;
        
        RoomOpening.Direction entryDirection = RoomOpening.Direction.North;
        RoomOpening.Direction exitDirection = RoomOpening.Direction.South;
        
        // Check X-axis penetration (corridor along Z axis)
        if (corridorSize.z > roomSize.x * 0.8f)
        {
            float corridorCenterX = corridorBounds.center.x;
            float roomCenterX = roomBounds.center.x;
            
            if (Mathf.Abs(corridorCenterX - roomCenterX) < roomSize.x * 0.3f)
            {
                entryDirection = RoomOpening.Direction.North;
                exitDirection = RoomOpening.Direction.South;
            }
        }
        
        // Check Z-axis penetration (corridor along X axis)
        else if (corridorSize.z > roomSize.z * 0.8f)
        {
            float corridorCenterZ = corridorBounds.center.z;
            float roomCenterZ = roomBounds.center.z;
            
            if (Mathf.Abs(corridorCenterZ - roomCenterZ) < roomSize.z * 0.3f)
            {
                entryDirection = RoomOpening.Direction.East;
                exitDirection = RoomOpening.Direction.West;
            }
        }

        // Create openings at the entry and exit points
        RoomOpening entryOpening = CreateSkeweredOpening(room, entryDirection, corridorBounds.min);
        RoomOpening exitOpening = CreateSkeweredOpening(room, exitDirection, corridorBounds.max);

        if (entryOpening != null && exitOpening != null)
        {
            // Mark openings as connected to each other
            entryOpening.MarkConnected(exitOpening);
            exitOpening.MarkConnected(entryOpening);
            
            // Add to extra connections so they're included in the adjacency graph
            extraOpenings.Add((entryOpening, exitOpening));
            
            Debug.Log($"Created skewered connection in {room.gameObject.name}: {entryDirection} -> {exitDirection}");
        }
    }

    private RoomOpening CreateSkeweredOpening(RoomGen room, RoomOpening.Direction direction, Vector3 corridorPoint)
    {
        Bounds roomBounds = CalculateRoomBounds(room.gameObject);
        Vector3 openingPosition = corridorPoint;
        
        // Clamp the opening position to the room bounds
        openingPosition.x = Mathf.Clamp(openingPosition.x, roomBounds.min.x, roomBounds.max.x);
        openingPosition.z = Mathf.Clamp(openingPosition.z, roomBounds.min.z, roomBounds.max.z);
        openingPosition.y = roomBounds.center.y;

        // Create the opening
        GameObject openingGO = new GameObject($"SkeweredOpening_{direction}");
        openingGO.transform.SetParent(room.transform, false);
        openingGO.transform.position = openingPosition;
        
        Quaternion rotation = direction switch
        {
            RoomOpening.Direction.North => Quaternion.LookRotation(Vector3.forward),
            RoomOpening.Direction.East => Quaternion.LookRotation(Vector3.right),
            RoomOpening.Direction.South => Quaternion.LookRotation(Vector3.back),
            RoomOpening.Direction.West => Quaternion.LookRotation(Vector3.left),
            _ => Quaternion.identity
        };
        
        openingGO.transform.rotation = rotation;

        RoomOpening opening = openingGO.AddComponent<RoomOpening>();
        typeof(RoomOpening).GetField("direction", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(opening, direction);

        return opening;
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

    private void DestroyIsolatedRooms()
    {
        List<RoomGen> roomsToDestroy = new();
        
        // Combine all connections (tree + extra)
        List<(RoomOpening A, RoomOpening B)> allConnections = new();
        allConnections.AddRange(treeOpenings);
        allConnections.AddRange(extraOpenings);
        
        Debug.Log($"DestroyIsolatedRooms: Checking {generatedRooms.Count} rooms with {allConnections.Count} total connections");
        Debug.Log($"Tree connections: {treeOpenings.Count}, Extra connections: {extraOpenings.Count}");
        
        // Build adjacency to find connected rooms
        Dictionary<RoomGen, List<RoomGen>> adjacency = new();
        foreach (RoomGen room in generatedRooms.Where(r => r != null))
        {
            adjacency[room] = new List<RoomGen>();
        }
        
        foreach ((RoomOpening A, RoomOpening B) edge in allConnections)
        {
            if (!IsValidEdge(edge)) 
            {
                Debug.Log($"Invalid edge detected: A={edge.A}, B={edge.B}");
                continue;
            }
            
            RoomGen roomA = GetRoomFromOpening(edge.A);
            RoomGen roomB = GetRoomFromOpening(edge.B);
            
            if (roomA != null && roomB != null)
            {
                adjacency[roomA].Add(roomB);
                adjacency[roomB].Add(roomA);
                Debug.Log($"Connection found: {roomA.gameObject.name} <-> {roomB.gameObject.name}");
            }
            else
            {
                Debug.Log($"Connection has null room: roomA={roomA}, roomB={roomB}");
            }
        }
        
        // Find isolated rooms (rooms with no connections)
        foreach (RoomGen room in generatedRooms.Where(r => r != null))
        {
            if (adjacency.TryGetValue(room, out List<RoomGen> connections))
            {
                Debug.Log($"Room {room.gameObject.name} has {connections.Count} connections");
                if (connections.Count == 0)
                {
                    roomsToDestroy.Add(room);
                    Debug.LogWarning($"Found isolated room: {room.gameObject.name}, marking for destruction");
                }
            }
            else
            {
                // Room not in adjacency list means it has no connections
                roomsToDestroy.Add(room);
                Debug.LogWarning($"Room not in adjacency list: {room.gameObject.name}, marking for destruction");
            }
        }
        
        Debug.Log($"Total isolated rooms to destroy: {roomsToDestroy.Count}");
        
        // Destroy isolated rooms and clean up references
        foreach (RoomGen room in roomsToDestroy)
        {
            if (room != null && room.gameObject != null)
            {
                Debug.LogWarning($"Destroying isolated room: {room.gameObject.name}");
                
                // Remove from collections
                generatedRooms.Remove(room);
                
                // Remove from bounds cache
                boundsCache.Remove(room.gameObject);
                
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

                ReturnToPool(room.gameObject);
            }
        }
        
        if (roomsToDestroy.Count > 0)
        {
            Debug.LogWarning($"Destroyed {roomsToDestroy.Count} isolated rooms. Remaining rooms: {generatedRooms.Count}");
        }
        else
        {
            Debug.Log("No isolated rooms found to destroy.");
        }
    }
}
