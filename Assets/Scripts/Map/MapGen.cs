using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MapGen : MonoBehaviour
{
    // Static event that other scripts can subscribe to
    public static System.Action OnMapGenerationComplete;
    
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
    [SerializeField] private float rotationVariety = 0.8f; // Probability to try different rotations
    [SerializeField] private float fullIntersectionThreshold = 0.7f; // 70% of room volume must be intersected to remove
    [SerializeField] private float openingProximityThreshold = 22f; // Minimum distance from openings to corridors/other rooms
    
    [Header("Tree Generation Settings")]
    [SerializeField] private float branchProbability = 0.7f;
    [SerializeField] private int maxTreeDepth = 5;
    [SerializeField] private int minBranchesPerNode = 1;
    [SerializeField] private int maxBranchesPerNode = 3;
    

    [Header("Regeneration Settings")]
    [SerializeField] private int maxRegenerationAttempts = 3;
    private int currentRegenerationAttempt = 0;
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
        currentRegenerationAttempt = 0; // Reset regeneration counter for new generation
        
        // Auto-assign room prefabs if not set

        if (roomPrefabs == null || roomPrefabs.Length == 0)
        {
            Debug.Log("[MapGen] No room prefabs assigned, trying to auto-find room prefabs");
            roomPrefabs = FindRoomPrefabs();
        }
        
        Debug.Log($"[MapGen] Starting tree-based dungeon generation. Room prefabs: {(roomPrefabs?.Length ?? 0)}");        

        if (roomPrefabs == null || roomPrefabs.Length == 0)
        {
            Debug.LogError("[MapGen] No room prefabs assigned and none found!");
            return;
        }           

        // Place root room at origin with random 90-degree rotation
        GameObject rootPrefab = roomPrefabs[Random.Range(0, roomPrefabs.Length)];
        Quaternion rootRotation = Quaternion.Euler(0, Random.Range(0, 4) * 90, 0);
        Debug.Log($"[MapGen] Placing root room: {rootPrefab.name} with rotation {rootRotation.eulerAngles.y} degrees (Variety: {rotationVariety})");
        RoomGen rootRoom = SpawnRoom(rootPrefab, Vector3.zero, rootRotation);
       
        if (rootRoom == null) 
        {
            Debug.LogError("[MapGen] Failed to spawn root room!");
            return;
        }        

        // Check root room openings for proximity issues (though there are no corridors yet)
        List<RoomOpening> rootOpenings = GetRoomOpenings(rootRoom);
        Debug.Log($"[MapGen] Root room spawned successfully. Openings: {rootOpenings.Count}");
       
        // Start tree growth from root
        GrowTreeFromRoom(rootRoom, 0);       
        Debug.Log($"[MapGen] Tree generation complete. Rooms generated: {generatedRooms.Count}, Connections: {connections.Count}");
       
        // Create corridors for all connections
        CreateCorridors();

        // Remove rooms that are fully intersected by corridors
        RemoveFullyIntersectedRooms();

        // Check and handle rooms with openings too close to corridors
        ValidateRoomOpeningProximity();

        // Find and rename rooms with furthest traversal distance
        RenameFurthestRooms();

        // Set difficulty for all rooms based on distance from Last Room
        SetRoomDifficulties();

        // Initialize spawning for all rooms (enemies, objects, crystals)
        InitializeRoomSpawning();

        // Notify all subscribers that map generation is complete
        OnMapGenerationComplete?.Invoke();
        
        // Disable this component after generation is complete
        enabled = false;
    }
    
    private void GrowTreeFromRoom(RoomGen parentRoom, int currentDepth)
    {
        if (currentDepth >= maxTreeDepth || generatedRooms.Count >= maxRooms)
        {
            Debug.Log($"[MapGen] Tree growth stopped at depth {currentDepth}. Max depth: {maxTreeDepth}, Max rooms: {maxRooms}");
            return;
        }

        Debug.Log($"[MapGen] Growing tree from room at depth {currentDepth}");

        // Get available openings from parent room
        var parentOpenings = GetRoomOpenings(parentRoom).Where(o => !o.IsConnected).ToList();

        if (parentOpenings.Count == 0)
        {
            Debug.Log($"[MapGen] No available openings in parent room at depth {currentDepth}");
            return;
        }        

        // Determine number of branches to create
        int maxPossibleBranches = Mathf.Min(parentOpenings.Count, maxBranchesPerNode);
        int numBranches = Random.Range(minBranchesPerNode, maxPossibleBranches + 1);

        // Randomly select openings to branch from
        var selectedOpenings = parentOpenings.OrderBy(x => Random.value).Take(numBranches).ToList();

        foreach (var opening in selectedOpenings)
        {
            // Check if we should branch based on probability
            if (Random.value > branchProbability && currentDepth > 0)
            {
                Debug.Log($"[MapGen] Skipping branch at depth {currentDepth} due to probability");
                opening.Seal(wallPrefab, transform);
                continue;
            }

            Debug.Log($"[MapGen] Attempting to grow branch from opening {opening.FacingDirection} at depth {currentDepth}");

            if (TryConnectRoom(opening, out RoomGen newRoom, out RoomOpening newOpening))
            {
                Debug.Log($"[MapGen] Successfully grew branch to new room at depth {currentDepth + 1}");
                
                // Mark connections
                opening.MarkConnected(newOpening);
                newOpening.MarkConnected(opening);
                connections.Add((opening, newOpening));
                
                // Recursively grow from the new room
                GrowTreeFromRoom(newRoom, currentDepth + 1);
            }

            else
            {
                Debug.Log($"[MapGen] Failed to grow branch, sealing opening at depth {currentDepth}");
                opening.Seal(wallPrefab, transform);
            }
        }

        // Seal any remaining unused openings
        var unusedOpenings = GetRoomOpenings(parentRoom).Where(o => !o.IsConnected);
        foreach (var unusedOpening in unusedOpenings)
        {
            unusedOpening.Seal(wallPrefab, transform);
        }
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

            Debug.Log($"[MapGen] Trying multiplier {multiplier} at exact distance {distance}");

            if (TryPlaceRoomAtPosition(targetPosition, targetDirection, fromOpening, out newRoom, out newOpening))
            {
                Debug.Log($"[MapGen] Successfully placed room with multiplier {multiplier} at exact {distance} units");
                return true;
            }
        }
        Debug.Log($"[MapGen] Failed to connect room after trying all multipliers");
        return false;
    }

    private bool TryPlaceRoomAtPosition(Vector3 position, RoomOpening.Direction requiredDirection, RoomOpening fromOpening, out RoomGen newRoom, out RoomOpening newOpening)

    {

        newRoom = null;

        newOpening = null;

        

        for (int attempt = 0; attempt < maxAttemptsPerRoom; attempt++)

        {

            GameObject prefab = roomPrefabs[Random.Range(0, roomPrefabs.Length)];

            if (prefab == null) continue;

            

            Debug.Log($"[MapGen] TryPlaceRoomAtPosition: Attempt {attempt + 1}/{maxAttemptsPerRoom} with prefab {prefab.name}");

            

            // Determine rotation strategy based on rotationVariety

            Quaternion[] rotationOptions;

            

            if (Random.value < rotationVariety)

            {

                // High variety: try all 4 rotations in random order

                Quaternion[] allRotations = { 

                    Quaternion.identity, 

                    Quaternion.Euler(0, 90, 0), 

                    Quaternion.Euler(0, 180, 0), 

                    Quaternion.Euler(0, 270, 0) 

                };

                rotationOptions = allRotations.OrderBy(x => Random.value).ToArray();

                Debug.Log($"[MapGen] Using high rotation variety (all 4 rotations)");

            }

            else

            {

                // Low variety: try 1-2 random rotations

                Quaternion[] allRotations = { 

                    Quaternion.identity, 

                    Quaternion.Euler(0, 90, 0), 

                    Quaternion.Euler(0, 180, 0), 

                    Quaternion.Euler(0, 270, 0) 

                };

                int numRotations = Random.Range(1, 3); // 1 or 2 rotations

                rotationOptions = allRotations.OrderBy(x => Random.value).Take(numRotations).ToArray();

                Debug.Log($"[MapGen] Using low rotation variety ({numRotations} rotations)");

            }

            

            // Try each rotation for this prefab

            foreach (Quaternion rotation in rotationOptions)

            {

                // Create temporary room instance with rotation

                GameObject tempRoom = Instantiate(prefab, Vector3.zero, rotation, transform);

                RoomGen roomGen = tempRoom.GetComponent<RoomGen>() ?? tempRoom.AddComponent<RoomGen>();

                

                // Find required opening

                List<RoomOpening> openings = GetRoomOpenings(roomGen);

                RoomOpening targetOpening = openings.FirstOrDefault(o => o.FacingDirection == requiredDirection);

                

                Debug.Log($"[MapGen] Room has {openings.Count} openings, looking for {requiredDirection} with rotation {rotation.eulerAngles.y}");

                

                if (targetOpening == null)

                {

                    Debug.Log($"[MapGen] No opening found for direction {requiredDirection} with rotation {rotation.eulerAngles.y}");

                    DestroyImmediate(tempRoom);

                    continue;

                }

                

                // Position room so target opening is at the desired position

                Vector3 localTargetPos = tempRoom.transform.InverseTransformPoint(targetOpening.transform.position);

                tempRoom.transform.position = position - tempRoom.transform.TransformVector(localTargetPos);

                

                // Verify corridor direction alignment (relaxed check)

                Vector3 actualOpeningPos = targetOpening.transform.position;

                Vector3 corridorDirection = (actualOpeningPos - fromOpening.transform.position).normalized;

                

                // Validate exact distance

                float actualDistance = Vector3.Distance(actualOpeningPos, fromOpening.transform.position);

                float expectedDistance = Vector3.Distance(position, fromOpening.transform.position);

                bool isExactDistance = Mathf.Abs(actualDistance - expectedDistance) < 0.1f;

                

                Debug.Log($"[MapGen] Distance validation: expected={expectedDistance:F2}, actual={actualDistance:F2}, exact={isExactDistance}");

                

                if (!isExactDistance)

                {

                    Debug.Log($"[MapGen] Distance mismatch, rejecting placement");

                    DestroyImmediate(tempRoom);

                    continue;

                }

                

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

                

                // Check opening proximity to corridors and other rooms

                if (AreOpeningsTooClose(openings, tempRoom, targetOpening))

                {

                    Debug.Log($"[MapGen] Room openings too close to corridors/rooms, rejecting");

                    DestroyImmediate(tempRoom);

                    continue;

                }

                

                // Success!

                generatedRooms.Add(roomGen);

                this.roomBounds.Add(roomBounds);

                newRoom = roomGen;

                newOpening = targetOpening;

                Debug.Log($"[MapGen] Successfully placed room with rotation {rotation.eulerAngles.y} degrees");

                return true;

            }

        }

        

        return false;

    }

    private void CreateCorridors()
    {
        // Create a copy of connections to avoid modification during enumeration
        var connectionsCopy = new List<(RoomOpening from, RoomOpening to)>(connections);
        foreach (var (fromOpening, toOpening) in connectionsCopy)
        {
            // Check if RoomOpening objects are still valid before accessing their transform
            if (fromOpening != null && fromOpening.gameObject != null && 
                toOpening != null && toOpening.gameObject != null)
            {
                CreateCorridor(fromOpening.transform.position, toOpening.transform.position);
            }
            else
            {
                Debug.LogWarning("[MapGen] Skipping corridor creation due to destroyed RoomOpening");
            }
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

        // Ensure we use exact multiples of corridorSpacing
        int segmentCount = Mathf.RoundToInt(length / corridorSpacing);
        Debug.Log($"[MapGen] Creating corridor: length={length:F2}, segments={segmentCount}, spacing={corridorSpacing}");

        // Place segments starting exactly from the opening position
        for (int i = 0; i < segmentCount; i++)
        {
            // Each segment is exactly corridorSpacing long
            Vector3 segmentStart = start + normalizedDirection * (i * corridorSpacing);
            Vector3 segmentEnd = start + normalizedDirection * ((i + 1) * corridorSpacing);
            Vector3 segmentCenter = (segmentStart + segmentEnd) * 0.5f;
            
            // Create temporary corridor segment to check bounds
            GameObject tempCorridor = Instantiate(corridorPrefab, segmentCenter, rotation, transform);
            Bounds corridorBounds = GetRoomBounds(tempCorridor);
            
            // Check if this corridor segment would clip with existing corridors
            if (WouldCorridorClip(corridorBounds, tempCorridor))
            {
                DestroyImmediate(tempCorridor);
                Debug.Log($"[MapGen] Corridor clipping detected at segment {i}, triggering map regeneration");
                TriggerMapRegeneration();
                return;
            }
            
            // If no clipping, keep the segment
            tempCorridor.name = $"Corridor_Segment_{i}";
            Debug.Log($"[MapGen] Placed segment {i} at {segmentCenter}");
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

    

    private bool WouldCorridorClip(Bounds candidateBounds, GameObject currentCorridor = null)
    {
        foreach (Transform child in transform)
        {
            if (child.name.Contains("Corridor") && child.gameObject != currentCorridor)
            {
                Bounds existingCorridorBounds = GetRoomBounds(child.gameObject);
                if (existingCorridorBounds.Intersects(candidateBounds))
                {
                    // Calculate intersection volume
                    Bounds intersection = existingCorridorBounds;
                    intersection.min = Vector3.Max(existingCorridorBounds.min, candidateBounds.min);
                    intersection.max = Vector3.Min(existingCorridorBounds.max, candidateBounds.max);
                    
                    Vector3 intersectionSize = intersection.size;
                    float intersectionVolume = intersectionSize.x * intersectionSize.y * intersectionSize.z;
                    
                    // If intersection is significant, consider it a clip
                    if (intersectionVolume > 1.0f) // A small threshold to allow for minor overlaps
                    {
                        Debug.Log($"[MapGen] Corridor clipping detected: {intersectionVolume} units³");
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private bool AreOpeningsTooClose(List<RoomOpening> openings, GameObject tempRoom, RoomOpening excludedOpening)
    {
        // Check each opening against corridors and other rooms
        foreach (RoomOpening opening in openings)
        {
            // Skip the opening that's being used for connection
            if (opening == excludedOpening) continue;

            Vector3 openingPos = opening.transform.position;

            // Check proximity to existing corridors
            foreach (Transform child in transform)
            {
                if (child.name.Contains("Corridor"))
                {
                    Bounds corridorBounds = GetRoomBounds(child.gameObject);

                    // Check if opening is too close to this corridor
                    if (IsPointNearBounds(openingPos, corridorBounds, openingProximityThreshold))
                    {
                        Debug.Log($"[MapGen] Opening at {openingPos} too close to corridor {child.name}");
                        return true;
                    }
                }
            }

            // Check proximity to existing rooms (excluding the temp room itself)
            foreach (RoomGen existingRoom in generatedRooms)
            {
                if (existingRoom.gameObject == tempRoom) continue;

                Bounds existingRoomBounds = GetRoomBounds(existingRoom.gameObject);

                // Check if opening is too close to this room
                if (IsPointNearBounds(openingPos, existingRoomBounds, openingProximityThreshold))
                {
                    Debug.Log($"[MapGen] Opening at {openingPos} too close to room {existingRoom.gameObject.name}");
                    return true;
                }
            }
        }
        return false;
    }

    private bool IsPointNearBounds(Vector3 point, Bounds bounds, float threshold)
    {
        // Expand the bounds by the threshold
        Bounds expandedBounds = new Bounds(bounds.center, bounds.size + Vector3.one * threshold * 2);

        // Check if point is inside the expanded bounds
        return expandedBounds.Contains(point);
    }

    private void RemoveFullyIntersectedRooms()
    {
        List<RoomGen> roomsToRemove = new List<RoomGen>();

        foreach (RoomGen room in generatedRooms)
        {
            // Skip if room is already destroyed
            if (room == null || room.gameObject == null) continue;

            if (IsRoomFullyIntersected(room))
            {
                roomsToRemove.Add(room);
                Debug.Log($"[MapGen] Marking room '{room.gameObject.name}' for removal due to full corridor intersection");
            }
        }

        // Remove the identified rooms
        foreach (RoomGen room in roomsToRemove)
        {
            // Double-check room still exists before removing
            if (room != null && room.gameObject != null)
            {
                RemoveRoom(room);
            }
        }
        
        if (roomsToRemove.Count > 0)
        {
            Debug.Log($"[MapGen] Removed {roomsToRemove.Count} rooms that were fully intersected by corridors");
        }
    }

    private bool IsRoomFullyIntersected(RoomGen room)
    {
        // Check if room is still valid
        if (room == null || room.gameObject == null) return false;

        Bounds roomBounds = GetRoomBounds(room.gameObject);
        float roomVolume = roomBounds.size.x * roomBounds.size.y * roomBounds.size.z;
        float totalIntersectionVolume = 0f;

        // Check intersection with all corridors
        foreach (Transform child in transform)
        {
            if (child.name.Contains("Corridor"))
            {
                Bounds corridorBounds = GetRoomBounds(child.gameObject);

                if (roomBounds.Intersects(corridorBounds))
                {
                    // Calculate intersection volume
                    Bounds intersection = roomBounds;
                    intersection.min = Vector3.Max(roomBounds.min, corridorBounds.min);
                    intersection.max = Vector3.Min(roomBounds.max, corridorBounds.max);

                    Vector3 intersectionSize = intersection.size;
                    float intersectionVolume = intersectionSize.x * intersectionSize.y * intersectionSize.z;
                    totalIntersectionVolume += intersectionVolume;
                }
            }
        }

        // Check if total intersection exceeds threshold
        float intersectionRatio = totalIntersectionVolume / roomVolume;
        bool isFullyIntersected = intersectionRatio >= fullIntersectionThreshold;

        Debug.Log($"[MapGen] Room '{room.gameObject.name}' intersection ratio: {intersectionRatio:P2} (threshold: {fullIntersectionThreshold:P2})");

        return isFullyIntersected;
    }

    private void RemoveRoom(RoomGen room)
    {
        // Check if room is still valid
        if (room == null || room.gameObject == null) return;

        string roomName = room.gameObject.name; // Store name before destruction
        
        // Remove from generated rooms list
        generatedRooms.Remove(room);

        // Remove from room bounds list
        Bounds roomBounds = GetRoomBounds(room.gameObject);
        this.roomBounds.Remove(roomBounds);

        // Remove any connections involving this room
        connections.RemoveAll(connection => 
            connection.from.GetComponentInParent<RoomGen>() == room || 
            connection.to.GetComponentInParent<RoomGen>() == room);

        // Destroy the room object
        DestroyImmediate(room.gameObject);
        Debug.Log($"[MapGen] Removed room '{roomName}' and cleaned up connections");
    }

    private void ValidateRoomOpeningProximity()
    {
        List<RoomGen> roomsToSeal = new List<RoomGen>();
        int totalProblematicOpenings = 0;

        foreach (RoomGen room in generatedRooms)
        {

            // Skip if room is already destroyed

            if (room == null || room.gameObject == null) continue;

            

            List<RoomOpening> openings = GetRoomOpenings(room);

            List<RoomOpening> problematicOpenings = new List<RoomOpening>();

            

            foreach (RoomOpening opening in openings)

            {

                // Skip connected openings

                if (opening.IsConnected) continue;

                

                // Check if this opening is too close to any corridor

                if (IsOpeningTooCloseToCorridors(opening))

                {

                    problematicOpenings.Add(opening);

                    Debug.Log($"[MapGen] Found problematic opening in room '{room.gameObject.name}' too close to corridors");

                }

            }

            

            // Seal problematic openings

            foreach (RoomOpening opening in problematicOpenings)

            {

                opening.Seal(wallPrefab, transform);

                Debug.Log($"[MapGen] Sealed problematic opening in room '{room.gameObject.name}'");

            }

            

            totalProblematicOpenings += problematicOpenings.Count;

            

            // If all openings are sealed, consider removing the room

            List<RoomOpening> allOpenings = GetRoomOpenings(room);

            bool allSealed = allOpenings.TrueForAll(o => o.IsConnected);

            

            if (allSealed && allOpenings.Count > 0)

            {

                roomsToSeal.Add(room);

                Debug.Log($"[MapGen] Room '{room.gameObject.name}' has all openings sealed, marking for potential removal");

            }

        }

        

        // Optionally remove rooms with all sealed openings (uncomment if desired)

        /*

        foreach (RoomGen room in roomsToSeal)

        {

            if (room != rootRoom) // Don't remove the root room

            {

                RemoveRoom(room);

                Debug.Log($"[MapGen] Removed room '{room.gameObject.name}' with all openings sealed");

            }

        }

        */

        

        if (totalProblematicOpenings > 0)

        {

            Debug.Log($"[MapGen] Sealed {totalProblematicOpenings} problematic openings due to corridor proximity");

        }

    }

    

    private bool IsOpeningTooCloseToCorridors(RoomOpening opening)

    {

        Vector3 openingPos = opening.transform.position;

        

        // Check proximity to all corridors

        foreach (Transform child in transform)

        {

            if (child.name.Contains("Corridor"))

            {

                Bounds corridorBounds = GetRoomBounds(child.gameObject);

                

                // Check if opening is too close to this corridor

                if (IsPointNearBounds(openingPos, corridorBounds, openingProximityThreshold))

                {

                    return true;

                }

            }

        }

        

        return false;

    }

    

    private RoomGen SpawnRoom(GameObject prefab, Vector3 position, Quaternion rotation)

    {

        GameObject roomObj = Instantiate(prefab, position, rotation, transform);

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

    

    private void RenameFurthestRooms()

    {

        if (generatedRooms.Count < 2)

        {

            Debug.LogWarning("[MapGen] Not enough rooms to find furthest traversal distance");

            return;

        }

        

        // Build adjacency list for the dungeon graph

        var adjacencyList = BuildAdjacencyList();

        

        // Find the pair of rooms with the maximum shortest path distance

        var (firstRoom, lastRoom, maxDistance) = FindFurthestRooms(adjacencyList);

        

        if (firstRoom != null && lastRoom != null)

        {

            firstRoom.gameObject.name = "First Room";

            lastRoom.gameObject.name = "Last Room";

            

            Debug.Log($"[MapGen] Renamed rooms: '{firstRoom.gameObject.name}' and '{lastRoom.gameObject.name}' with traversal distance {maxDistance}");

        }

        else

        {

            Debug.LogWarning("[MapGen] Could not find furthest rooms");

        }

    }

    

    private void SetRoomDifficulties()

    {

        // Find the Last Room

        RoomGen lastRoom = generatedRooms.FirstOrDefault(room => room.gameObject.name == "Last Room");

        

        if (lastRoom == null)

        {

            Debug.LogWarning("[MapGen] Could not find Last Room for difficulty setting");

            return;

        }

        

        // Build adjacency list for distance calculations

        var adjacencyList = BuildAdjacencyList();

        

        // Calculate distances from Last Room to all other rooms

        var distancesFromLast = BFS(lastRoom, adjacencyList);

        

        // Sort rooms by distance from Last Room

        var sortedRooms = distancesFromLast

            .Where(kvp => kvp.Value >= 0) // Only reachable rooms

            .OrderBy(kvp => kvp.Value)

            .ToList();

        

        int totalRooms = sortedRooms.Count;

        

        // Set difficulty for each room based on its percentage position in the sorted list

        for (int i = 0; i < sortedRooms.Count; i++)

        {

            var room = sortedRooms[i].Key;

            var distance = sortedRooms[i].Value;

            

            // Calculate difficulty as percentage (0-100) based on room position

            float difficultyPercentage = (float)i / (totalRooms - 1) * 100f;

            int difficulty = Mathf.RoundToInt(difficultyPercentage);

            

            room.SetDifficulty(difficulty);

            Debug.Log($"[MapGen] Set difficulty for room '{room.gameObject.name}': {difficulty} (position {i+1}/{totalRooms}, distance {distance} from Last Room)");

        }

        

        // Handle unreachable rooms

        foreach (var kvp in distancesFromLast)

        {

            if (kvp.Value < 0) // Unreachable room

            {

                var room = kvp.Key;

                room.SetDifficulty(10); // Set to easiest difficulty

                Debug.LogWarning($"[MapGen] Room '{room.gameObject.name}' is unreachable from Last Room, setting easiest difficulty");

            }

        }

    }

    

    private Dictionary<RoomGen, List<RoomGen>> BuildAdjacencyList()

    {

        var adjacencyList = new Dictionary<RoomGen, List<RoomGen>>();

        

        // Initialize all rooms with empty lists

        foreach (var room in generatedRooms)

        {

            adjacencyList[room] = new List<RoomGen>();

        }

        

        // Add connections based on room openings

        foreach (var (fromOpening, toOpening) in connections)

        {

            var fromRoom = fromOpening.GetComponentInParent<RoomGen>();

            var toRoom = toOpening.GetComponentInParent<RoomGen>();

            

            if (fromRoom != null && toRoom != null && fromRoom != toRoom)

            {

                if (!adjacencyList[fromRoom].Contains(toRoom))

                {

                    adjacencyList[fromRoom].Add(toRoom);

                }

                if (!adjacencyList[toRoom].Contains(fromRoom))
                {
                    adjacencyList[toRoom].Add(fromRoom);
                }
            }
        }
        
        return adjacencyList;
    }
    
    private (RoomGen firstRoom, RoomGen lastRoom, int maxDistance) FindFurthestRooms(Dictionary<RoomGen, List<RoomGen>> adjacencyList)
    {
        RoomGen firstRoom = null;
        RoomGen lastRoom = null;
        int maxDistance = -1;
        
        // Calculate shortest paths between all pairs of rooms using BFS
        foreach (var startRoom in generatedRooms)
        {
            var distances = BFS(startRoom, adjacencyList);
            
            foreach (var kvp in distances)
            {
                var endRoom = kvp.Key;
                var distance = kvp.Value;
                
                if (distance > maxDistance)

                {

                    maxDistance = distance;

                    firstRoom = startRoom;

                    lastRoom = endRoom;

                }

            }

        }

        

        return (firstRoom, lastRoom, maxDistance);

    }

    

    private Dictionary<RoomGen, int> BFS(RoomGen startRoom, Dictionary<RoomGen, List<RoomGen>> adjacencyList)

    {

        var distances = new Dictionary<RoomGen, int>();

        var queue = new Queue<RoomGen>();

        

        // Initialize distances

        foreach (var room in generatedRooms)

        {

            distances[room] = -1; // -1 means unreachable

        }

        

        distances[startRoom] = 0;

        queue.Enqueue(startRoom);

        

        while (queue.Count > 0)

        {

            var currentRoom = queue.Dequeue();

            

            foreach (var neighbor in adjacencyList[currentRoom])

            {

                if (distances[neighbor] == -1) // Not visited yet

                {

                    distances[neighbor] = distances[currentRoom] + 1;

                    queue.Enqueue(neighbor);

                }

            }

        }

        

        return distances;

    }

    

    private void TriggerMapRegeneration()
    {
        currentRegenerationAttempt++;
        
        if (currentRegenerationAttempt <= maxRegenerationAttempts)
        {
            Debug.Log($"[MapGen] Corridor clipping detected! Regenerating map (attempt {currentRegenerationAttempt}/{maxRegenerationAttempts})");
            GenerateDungeon();
        }
        else
        {
            Debug.LogError($"[MapGen] Failed to generate map without corridor clipping after {maxRegenerationAttempts} attempts. Using current map.");
            currentRegenerationAttempt = 0; // Reset for next time
        }
    }

    /// <summary>
    /// Initializes spawning for all generated rooms (enemies, objects, crystals)
    /// </summary>
    private void InitializeRoomSpawning()
    {
        Debug.Log($"[MapGen] Initializing spawning for {generatedRooms.Count} rooms");
        
        foreach (RoomGen room in generatedRooms)
        {
            if (room != null)
            {
                room.InitializeSpawning();
            }
        }
        
        Debug.Log("[MapGen] Room spawning initialization complete");
    }
}

