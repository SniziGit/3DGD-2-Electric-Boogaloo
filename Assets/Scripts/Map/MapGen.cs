using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MapGen : MonoBehaviour
{
    [Header("Room Settings")]
    [SerializeField] private GameObject[] roomPrefabs;
    
    [Header("Corridor Settings")]
    [SerializeField] private GameObject corridorPrefab;
    [SerializeField] private float corridorSpacing = 22f;
    
    [Header("Generation Settings")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private int maxRooms = 25;
    [SerializeField] private int maxAttemptsPerRoom = 10;
    [SerializeField] private GameObject wallPrefab;
    
    private readonly List<RoomGen> generatedRooms = new();
    private readonly List<Bounds> roomBounds = new();
    private readonly List<(RoomOpening from, RoomOpening to)> connections = new();
    
    private void Start()
    {
        if (generateOnStart)
        {
            GenerateDungeon();
        }
    }
    
    private void GenerateDungeon()
    {
        ClearExistingDungeon();
        
        // Auto-assign room prefabs if not set
        if (roomPrefabs == null || roomPrefabs.Length == 0)
        {
            Debug.Log("[MapGen] No room prefabs assigned, trying to auto-find room prefabs");
            roomPrefabs = FindRoomPrefabs();
        }
        
        Debug.Log($"[MapGen] Starting dungeon generation. Room prefabs: {(roomPrefabs?.Length ?? 0)}");
        
        if (roomPrefabs == null || roomPrefabs.Length == 0)
        {
            Debug.LogError("[MapGen] No room prefabs assigned and none found!");
            return;
        }
            
        // Place starting room at origin
        GameObject startPrefab = roomPrefabs[Random.Range(0, roomPrefabs.Length)];
        Debug.Log($"[MapGen] Placing start room: {startPrefab.name}");
        RoomGen startRoom = SpawnRoom(startPrefab, Vector3.zero);
        
        if (startRoom == null) 
        {
            Debug.LogError("[MapGen] Failed to spawn start room!");
            return;
        }
        
        Debug.Log($"[MapGen] Start room spawned successfully. Openings: {GetRoomOpenings(startRoom).Count}");
        
        Queue<RoomOpening> pendingOpenings = new();
        foreach (RoomOpening opening in GetRoomOpenings(startRoom))
        {
            if (!opening.IsConnected)
            {
                pendingOpenings.Enqueue(opening);
                Debug.Log($"[MapGen] Added opening to queue: {opening.FacingDirection}");
            }
        }
        
        Debug.Log($"[MapGen] Starting generation loop. Pending openings: {pendingOpenings.Count}, Max rooms: {maxRooms}");
        
        // Generate dungeon
        int attempts = 0;
        while (pendingOpenings.Count > 0 && generatedRooms.Count < maxRooms)
        {
            RoomOpening currentOpening = pendingOpenings.Dequeue();
            if (currentOpening.IsConnected) continue;
            
            Debug.Log($"[MapGen] Attempting to connect from opening {currentOpening.FacingDirection}. Room count: {generatedRooms.Count}");
            
            if (TryConnectRoom(currentOpening, out RoomGen newRoom, out RoomOpening newOpening))
            {
                Debug.Log($"[MapGen] Successfully connected new room!");
                // Mark connections
                currentOpening.MarkConnected(newOpening);
                newOpening.MarkConnected(currentOpening);
                connections.Add((currentOpening, newOpening));
                
                // Add new room's openings to queue
                var newOpenings = GetRoomOpenings(newRoom);
                Debug.Log($"[MapGen] New room has {newOpenings.Count} openings");
                foreach (RoomOpening opening in newOpenings)
                {
                    if (!opening.IsConnected)
                    {
                        pendingOpenings.Enqueue(opening);
                        Debug.Log($"[MapGen] Added new opening: {opening.FacingDirection}");
                    }
                }
            }
            else
            {
                Debug.Log($"[MapGen] Failed to connect room, sealing opening");
                // Seal failed opening
                currentOpening.Seal(wallPrefab, transform);
            }
            
            attempts++;
            if (attempts > 100) // Prevent infinite loops
            {
                Debug.LogWarning("[MapGen] Generation loop exceeded 100 attempts, stopping");
                break;
            }
        }
        
        Debug.Log($"[MapGen] Generation complete. Rooms generated: {generatedRooms.Count}, Connections: {connections.Count}");
        
        // Create corridors for all connections
        CreateCorridors();
    }
    
    private bool TryConnectRoom(RoomOpening fromOpening, out RoomGen newRoom, out RoomOpening newOpening)
    {
        newRoom = null;
        newOpening = null;
        
        RoomOpening.Direction targetDirection = GetOppositeDirection(fromOpening.FacingDirection);
        Debug.Log($"[MapGen] TryConnectRoom: Looking for direction {targetDirection}");
        
        // Try different multipliers of corridor spacing
        int[] multipliers = { 1, 2, 3 }; // 22, 44, 66 units
        
        foreach (int multiplier in multipliers)
        {
            float distance = multiplier * corridorSpacing;
            Vector3 targetPosition = fromOpening.transform.position + fromOpening.transform.forward * distance;
            
            Debug.Log($"[MapGen] Trying multiplier {multiplier} at distance {distance}");
            
            if (TryPlaceRoomAtPosition(targetPosition, targetDirection, fromOpening, out newRoom, out newOpening))
            {
                Debug.Log($"[MapGen] Successfully placed room with multiplier {multiplier}");
                return true;
            }
        }
        
        Debug.Log($"[MapGen] Failed to connect room after trying all multipliers");
        return false;
    }
    
    private bool TryPlaceRoomAtPosition(Vector3 position, RoomOpening.Direction requiredDirection, 
                                       RoomOpening fromOpening, out RoomGen newRoom, out RoomOpening newOpening)
    {
        newRoom = null;
        newOpening = null;
        
        for (int attempt = 0; attempt < maxAttemptsPerRoom; attempt++)
        {
            GameObject prefab = roomPrefabs[Random.Range(0, roomPrefabs.Length)];
            if (prefab == null) continue;
            
            Debug.Log($"[MapGen] TryPlaceRoomAtPosition: Attempt {attempt + 1}/{maxAttemptsPerRoom} with prefab {prefab.name}");
            
            // Create temporary room instance
            GameObject tempRoom = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
            RoomGen roomGen = tempRoom.GetComponent<RoomGen>() ?? tempRoom.AddComponent<RoomGen>();
            
            // Find required opening
            List<RoomOpening> openings = GetRoomOpenings(roomGen);
            RoomOpening targetOpening = openings.FirstOrDefault(o => o.FacingDirection == requiredDirection);
            
            Debug.Log($"[MapGen] Room has {openings.Count} openings, looking for {requiredDirection}");
            
            if (targetOpening == null)
            {
                Debug.Log($"[MapGen] No opening found for direction {requiredDirection}");
                DestroyImmediate(tempRoom);
                continue;
            }
            
            // Position room so target opening is at the desired position
            Vector3 localTargetPos = tempRoom.transform.InverseTransformPoint(targetOpening.transform.position);
            tempRoom.transform.position = position - tempRoom.transform.TransformVector(localTargetPos);
            
            // Verify corridor direction alignment (relaxed check)
            Vector3 actualOpeningPos = targetOpening.transform.position;
            Vector3 corridorDirection = (actualOpeningPos - fromOpening.transform.position).normalized;
            
            if (Vector3.Dot(corridorDirection, fromOpening.transform.forward) < 0.5f)
            {
                Debug.Log($"[MapGen] Corridor direction alignment failed: dot={Vector3.Dot(corridorDirection, fromOpening.transform.forward)}");
                DestroyImmediate(tempRoom);
                continue;
            }
            
            // Check for clipping
            Bounds roomBounds = GetRoomBounds(tempRoom);
            Debug.Log($"[MapGen] Room bounds: {roomBounds.center}, size: {roomBounds.size}");
            
            if (WouldRoomClip(roomBounds))
            {
                Debug.Log($"[MapGen] Room would clip, rejecting");
                DestroyImmediate(tempRoom);
                continue;
            }
            
            // Success!
            generatedRooms.Add(roomGen);
            this.roomBounds.Add(roomBounds);
            newRoom = roomGen;
            newOpening = targetOpening;
            return true;
        }
        
        return false;
    }
    
    private void CreateCorridors()
    {
        foreach (var (fromOpening, toOpening) in connections)
        {
            CreateCorridor(fromOpening.transform.position, toOpening.transform.position);
        }
    }
    
    private void CreateCorridor(Vector3 start, Vector3 end)
    {
        if (corridorPrefab == null) return;
        
        Vector3 direction = end - start;
        direction.y = 0f;
        
        if (direction == Vector3.zero) return;
        
        float length = direction.magnitude;
        Vector3 normalizedDirection = direction.normalized;
        Quaternion rotation = Quaternion.LookRotation(normalizedDirection, Vector3.up) * Quaternion.Euler(0, 90, 0);
        
        // Calculate number of segments needed
        int segmentCount = Mathf.CeilToInt(length / corridorSpacing);
        float actualSegmentLength = corridorSpacing;
        
        // Adjust positioning to center segments
        float totalLength = segmentCount * actualSegmentLength;
        float startOffset = (totalLength - length) * 0.5f;
        Vector3 adjustedStart = start + normalizedDirection * startOffset;
        
        for (int i = 0; i < segmentCount; i++)
        {
            float segmentStart = i * actualSegmentLength;
            float segmentEnd = segmentStart + actualSegmentLength;
            
            Vector3 segmentStartPos = adjustedStart + normalizedDirection * segmentStart;
            Vector3 segmentEndPos = adjustedStart + normalizedDirection * segmentEnd;
            Vector3 segmentCenter = (segmentStartPos + segmentEndPos) * 0.5f;
            
            GameObject corridor = Instantiate(corridorPrefab, segmentCenter, rotation, transform);
            corridor.name = $"Corridor_Segment_{i}";
        }
    }
    
    private bool WouldRoomClip(Bounds candidateBounds)
    {
        // Check against existing rooms
        foreach (Bounds existingBounds in roomBounds)
        {
            if (existingBounds.Intersects(candidateBounds))
                return true;
        }
        
        // Check against corridors (allow small intersections for connections)
        foreach (Transform child in transform)
        {
            if (child.name.Contains("Corridor"))
            {
                Bounds corridorBounds = GetRoomBounds(child.gameObject);
                
                if (corridorBounds.Intersects(candidateBounds))
                {
                    // Calculate intersection volume
                    Bounds intersection = corridorBounds;
                    intersection.min = Vector3.Max(corridorBounds.min, candidateBounds.min);
                    intersection.max = Vector3.Min(corridorBounds.max, candidateBounds.max);
                    
                    Vector3 intersectionSize = intersection.size;
                    float intersectionVolume = intersectionSize.x * intersectionSize.y * intersectionSize.z;
                    
                    // Only flag as clipping if intersection is significant (relaxed)
                    if (intersectionVolume > 10f)
                    {
                        Debug.Log($"[MapGen] Significant clipping detected: {intersectionVolume} units³");
                        return true;
                    }
                }
            }
        }
        
        return false;
    }
    
    private RoomGen SpawnRoom(GameObject prefab, Vector3 position)
    {
        GameObject roomObj = Instantiate(prefab, position, Quaternion.identity, transform);
        RoomGen roomGen = roomObj.GetComponent<RoomGen>() ?? roomObj.AddComponent<RoomGen>();
        
        generatedRooms.Add(roomGen);
        roomBounds.Add(GetRoomBounds(roomObj));
        
        return roomGen;
    }
    
    private List<RoomOpening> GetRoomOpenings(RoomGen room)
    {
        RoomOpening[] existing = room.GetComponentsInChildren<RoomOpening>();
        if (existing.Length > 0)
        {
            return existing.ToList();
        }
        
        // Create openings automatically if they don't exist
        List<RoomOpening> openings = new();
        Bounds roomBounds = GetRoomBounds(room.gameObject);
        Vector3 roomCenter = room.transform.position;
        
        // Calculate room extents in local space
        Vector3 localExtents = room.transform.InverseTransformVector(roomBounds.extents);
        float halfWidth = Mathf.Abs(localExtents.x);
        float halfDepth = Mathf.Abs(localExtents.z);
        
        // Create openings on all four sides
        openings.Add(CreateOpening(room.transform, RoomOpening.Direction.North, new Vector3(0, 0, halfDepth), Quaternion.LookRotation(Vector3.forward)));
        openings.Add(CreateOpening(room.transform, RoomOpening.Direction.East, new Vector3(halfWidth, 0, 0), Quaternion.LookRotation(Vector3.right)));
        openings.Add(CreateOpening(room.transform, RoomOpening.Direction.South, new Vector3(0, 0, -halfDepth), Quaternion.LookRotation(Vector3.back)));
        openings.Add(CreateOpening(room.transform, RoomOpening.Direction.West, new Vector3(-halfWidth, 0, 0), Quaternion.LookRotation(Vector3.left)));
        
        return openings;
    }
    
    private RoomOpening CreateOpening(Transform roomTransform, RoomOpening.Direction direction, Vector3 localPos, Quaternion localRot)
    {
        GameObject go = new GameObject($"Opening_{direction}");
        go.transform.SetParent(roomTransform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;

        RoomOpening opening = go.AddComponent<RoomOpening>();
        opening.Initialize(direction);
        return opening;
    }
    
    private Bounds GetRoomBounds(GameObject room)
    {
        Renderer[] renderers = room.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }
        
        // Fallback to collider
        BoxCollider collider = room.GetComponentInChildren<BoxCollider>();
        if (collider != null)
            return collider.bounds;
            
        return new Bounds(room.transform.position, Vector3.one);
    }
    
    private RoomOpening.Direction GetOppositeDirection(RoomOpening.Direction direction)
    {
        return direction switch
        {
            RoomOpening.Direction.North => RoomOpening.Direction.South,
            RoomOpening.Direction.South => RoomOpening.Direction.North,
            RoomOpening.Direction.East => RoomOpening.Direction.West,
            RoomOpening.Direction.West => RoomOpening.Direction.East,
            _ => RoomOpening.Direction.North
        };
    }
    
    private void ClearExistingDungeon()
    {
        // Destroy all existing rooms and corridors
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        
        generatedRooms.Clear();
        roomBounds.Clear();
        connections.Clear();
    }
    
    private GameObject[] FindRoomPrefabs()
    {
        List<GameObject> foundPrefabs = new();
        
        // Search in common prefab directories
        string[] searchPaths = { "Assets/Prefab", "Assets/_Creepy_Cat/_3D Scifi Kit Starter Kit_HD/_Your_Hown_Prefabs" };
        
        foreach (string path in searchPaths)
        {
            string[] prefabFiles = System.IO.Directory.GetFiles(path, "*.prefab", System.IO.SearchOption.AllDirectories);
            
            foreach (string file in prefabFiles)
            {
                string assetPath = file.Replace('\\', '/');
                if (assetPath.Contains("Room"))
                {
                    #if UNITY_EDITOR
                    GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (prefab != null)
                    {
                        foundPrefabs.Add(prefab);
                        Debug.Log($"[MapGen] Found room prefab: {prefab.name}");
                    }
                    #endif
                }
            }
        }
        
        Debug.Log($"[MapGen] Auto-found {foundPrefabs.Count} room prefabs");
        return foundPrefabs.ToArray();
    }
}
