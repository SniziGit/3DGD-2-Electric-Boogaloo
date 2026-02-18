using UnityEngine;

public class DoorAnimTrigger : MonoBehaviour
{
    private Animator doorAnimator;
    private Collider doorCollider;
    [SerializeField] private float closeDelay = 1.0f;
    [SerializeField] private bool isLocked = false;
    [SerializeField] private bool hasPuzzle = false;
    [SerializeField] private Light[] lights;
    private bool isPlayerInside = false;
    private Coroutine closeCoroutine;

    void Start()
    {
        doorAnimator = GetComponent<Animator>();
        doorCollider = GetComponent<Collider>();
        
        // Check if door leads to corridor and set lock state accordingly
        bool leadsToCorridor = DoesDoorLeadToCorridor();
        bool isLastRoomDoor = IsLastRoomDoor();
        
        if (leadsToCorridor)
        {
            UnlockDoor();
            Debug.Log($"[DoorAnimTrigger] Unlocked door at {transform.position} (leads to corridor)");
            
            // 60% chance for unlocked doors to have puzzle and be locked
            if (Random.value < 0.6f)
            {
                hasPuzzle = true;
                LockDoor();
                SetLightsColor(Color.yellow);
                Debug.Log($"[DoorAnimTrigger] Door at {transform.position} now has puzzle and is locked (60% chance)");
            }
        }
        else
        {
            LockDoor();
            Debug.Log($"[DoorAnimTrigger] Locked door at {transform.position} (does not lead to corridor)");
        }
        
        // Last Room doors always have puzzle and are locked
        if (isLastRoomDoor)
        {
            hasPuzzle = true;
            LockDoor();
            SetLightsColor(Color.yellow);
            Debug.Log($"[DoorAnimTrigger] Last Room door at {transform.position} has puzzle and is locked");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isLocked)
        {
            isPlayerInside = true;
            doorAnimator.SetBool("isOpen", true);
            
            if (closeCoroutine != null)
            {
                StopCoroutine(closeCoroutine);
                closeCoroutine = null;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = false;
            closeCoroutine = StartCoroutine(CloseDoorAfterDelay());
        }
    }

    private System.Collections.IEnumerator CloseDoorAfterDelay()
    {
        yield return new WaitForSeconds(closeDelay);
        
        if (!isPlayerInside)
        {
            doorAnimator.SetBool("isOpen", false);
        }
        closeCoroutine = null;
    }

    public void UnlockDoor()
    {
        isLocked = false;
    }

    public void LockDoor()
    {
        isLocked = true;
        if (hasPuzzle)
        {
            SetLightsColor(Color.yellow);
        }
        else
        {
            SetLightsColor(Color.red);
        }
    }
    
    public bool HasPuzzle()
    {
        return hasPuzzle;
    }

    private void SetLightsColor(Color color)
    {
        if (lights != null)
        {
            foreach (Light light in lights)
            {
                if (light != null)
                {
                    light.color = color;
                }
            }
        }
    }
    
    private bool DoesDoorLeadToCorridor()
    {
        // Check if there's a corridor near this door
        Vector3 doorPosition = transform.position;
        float checkRadius = 7f; // Check within 5 units of the door
        
        // Find the MapGen object to check for corridors
        MapGen mapGen = FindFirstObjectByType<MapGen>();
        if (mapGen == null)
        {
            Debug.LogWarning("[DoorAnimTrigger] MapGen not found, assuming door does not lead to corridor");
            return false;
        }
        
        // Check for nearby corridor objects
        foreach (Transform child in mapGen.transform)
        {
            if (child.name.Contains("Corridor"))
            {
                Bounds corridorBounds = GetRoomBounds(child.gameObject);
                
                // Expand the bounds slightly for better detection
                Bounds expandedBounds = new Bounds(corridorBounds.center, corridorBounds.size + Vector3.one * checkRadius);
                
                if (expandedBounds.Contains(doorPosition))
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    private Bounds GetRoomBounds(GameObject room)
    {
        Bounds bounds = new Bounds(room.transform.position, Vector3.zero);
        
        // Get all renderers in the room and their children
        Renderer[] renderers = room.GetComponentsInChildren<Renderer>();
        
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        else
        {
            // Fallback: use collider bounds if no renderers
            Collider[] colliders = room.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                bounds = colliders[0].bounds;
                foreach (Collider collider in colliders)
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
        }
        
        return bounds;
    }
    
    private bool IsLastRoomDoor()
    {
        // Check if this door belongs to the Last Room
        Transform parent = transform.parent;
        while (parent != null)
        {
            if (parent.name == "Last Room")
            {
                return true;
            }
            parent = parent.parent;
        }
        
        return false;
    }
}
