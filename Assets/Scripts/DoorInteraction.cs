using UnityEngine;

public class DoorInteraction : MonoBehaviour
{
    [Header("Door Unlocking Puzzle")]
    [SerializeField] private float hackTime = 2f;
    [SerializeField] private float hackRange = 5f; // Increased from 3f to 5f to ensure it works
    
    private bool isHacking = false;
    private float hackTimer = 0f;
    private DoorAnimTrigger targetDoor;
    private FPSMovement playerMovement;
    
    void Start()
    {
        playerMovement = GetComponent<FPSMovement>();
    }
    
    void Update()
    {
        if (isHacking && targetDoor != null)
        {
            hackTimer += Time.deltaTime;
            
            // Check if target is still in range and still has puzzle
            // Use raycast to check actual distance to door collider instead of trigger position
            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, hackRange) && hit.collider.CompareTag("Door"))
            {
                // Check if this door still has puzzle
                DoorAnimTrigger door = hit.collider.GetComponentInParent<DoorAnimTrigger>();
                if (door == null)
                {
                    door = hit.collider.GetComponent<DoorAnimTrigger>();
                }
                
                if (door != targetDoor || !door.HasPuzzle())
                {
                    CancelHack();
                    return;
                }
            }
            else
            {
                CancelHack();
                return;
            }
            
            // Complete hack
            if (hackTimer >= hackTime)
            {
                CompleteHack();
            }
        }
    }
    
    public bool CanHack()
    {
        if (isHacking) 
        {
            Debug.Log("[DoorInteraction] Already hacking, cannot start new hack");
            return false;
        }
        
        Debug.Log($"[DoorInteraction] Checking if can hack - position: {transform.position}, forward: {transform.forward}, range: {hackRange}");
        
        // Raycast to check if player is looking at a door
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, hackRange))
        {
            Debug.Log($"[DoorInteraction] Raycast hit: {hit.collider.name} at distance {hit.distance}");
            
            if (hit.collider.CompareTag("Door"))
            {
                Debug.Log("[DoorInteraction] Hit object has 'Door' tag");
                
                // Try to find DoorAnimTrigger on the parent or the object itself
                DoorAnimTrigger door = hit.collider.GetComponentInParent<DoorAnimTrigger>();
                if (door == null)
                {
                    door = hit.collider.GetComponent<DoorAnimTrigger>();
                }
                
                if (door != null)
                {
                    Debug.Log($"[DoorInteraction] Found DoorAnimTrigger for {door.name}");
                    if (door.HasPuzzle())
                    {
                        Debug.Log("[DoorInteraction] Door has puzzle, can hack");
                        targetDoor = door;
                        return true;
                    }
                    else
                    {
                        Debug.Log("[DoorInteraction] Door does not have puzzle, cannot hack");
                    }
                }
                else
                {
                    Debug.Log("[DoorInteraction] No DoorAnimTrigger component found on door or parent");
                }
            }
            else
            {
                Debug.Log($"[DoorInteraction] Hit object tag is '{hit.collider.tag}', not 'Door'");
            }
        }
        else
        {
            Debug.Log("[DoorInteraction] Raycast hit nothing within hack range");
        }
        return false;
    }
    
    public void StartHack()
    {
        if (CanHack() && !isHacking)
        {
            isHacking = true;
            hackTimer = 0f;
        }
    }
    
    private void CompleteHack()
    {
        if (targetDoor != null)
        {
            targetDoor.UnlockDoor();
        }
        
        CancelHack();
    }
    
    public void CancelHack()
    {
        isHacking = false;
        hackTimer = 0f;
        targetDoor = null;
    }
    
    public bool IsHacking()
    {
        return isHacking;
    }
    
    public float GetHackProgress()
    {
        return isHacking ? hackTimer / hackTime : 0f;
    }
}
