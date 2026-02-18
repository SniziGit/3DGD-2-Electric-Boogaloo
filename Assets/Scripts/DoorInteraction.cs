using UnityEngine;

public class DoorInteraction : MonoBehaviour
{
    [Header("Door Unlocking Puzzle")]
    [SerializeField] private float hackTime = 2f;
    [SerializeField] private float hackRange = 2f;
    
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
            if (Vector3.Distance(transform.position, targetDoor.transform.position) > hackRange || !targetDoor.HasPuzzle())
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
        if (isHacking) return false;
        
        // Raycast to check if player is looking at a door
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, hackRange))
        {
            if (hit.collider.CompareTag("Door"))
            {
                DoorAnimTrigger door = hit.collider.GetComponent<DoorAnimTrigger>();
                if (door != null && door.HasPuzzle())
                {
                    targetDoor = door;
                    return true;
                }
            }
        }
        return false;
    }
    
    public void StartHack()
    {
        if (CanHack() && !isHacking)
        {
            isHacking = true;
            hackTimer = 0f;
            Debug.Log("Started hacking door...");
        }
    }
    
    private void CompleteHack()
    {
        if (targetDoor != null)
        {
            targetDoor.UnlockDoor();
            Debug.Log("Successfully hacked and unlocked door");
        }
        
        CancelHack();
    }
    
    public void CancelHack()
    {
        isHacking = false;
        hackTimer = 0f;
        targetDoor = null;
        Debug.Log("Door hack canceled");
    }
    
    public float GetHackProgress()
    {
        return isHacking ? hackTimer / hackTime : 0f;
    }
    
    public bool IsHacking()
    {
        return isHacking;
    }
}
