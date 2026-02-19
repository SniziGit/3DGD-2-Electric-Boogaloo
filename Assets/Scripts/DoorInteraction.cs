using UnityEngine;

public class DoorInteraction : MonoBehaviour
{
    [Header("Door Unlocking Puzzle")]
    [SerializeField] private float hackRange = 5f; // Increased from 3f to 5f to ensure it works
    [SerializeField] private float hackCooldownSeconds = 5f;
    [SerializeField] private int passwordLength = 4;
    [SerializeField] private float passwordDisplaySeconds = 2.5f;
    [SerializeField] private float hackHoldDuration = 2f; // Time required to hold before password appears
    
    [Header("Per-Player UI")]
    [SerializeField] private DirectionalSequenceUI passwordUi;

    private bool isHacking = false;
    private bool isHoldingHack = false;
    private float hackHoldTimer = 0f;
    private DoorAnimTrigger targetDoor;
    private DoorAnimTrigger lastDoorLookedAt;
    private FPSMovement playerMovement;
    private SequenceInputHandler sequenceInput;

    private static readonly System.Collections.Generic.Dictionary<DoorAnimTrigger, float> doorCooldownEndTime = new System.Collections.Generic.Dictionary<DoorAnimTrigger, float>();
    
    void Start()
    {
        playerMovement = GetComponent<FPSMovement>();
        sequenceInput = GetComponent<SequenceInputHandler>();
    }
    
    void Update()
    {
        // Update hold timer
        if (isHoldingHack && targetDoor != null)
        {
            hackHoldTimer += Time.deltaTime;
            
            // Check if hold is complete
            if (hackHoldTimer >= hackHoldDuration)
            {
                CompleteHackHold();
            }
            // Check if player stopped looking at door or moved away
            else if (!IsDoorStillHackable(targetDoor))
            {
                CancelHackHold();
            }
        }
        
        if (!isHacking || targetDoor == null)
            return;

        if (!IsDoorStillHackable(targetDoor))
        {
            CancelHack();
            return;
        }
    }

    private bool IsDoorStillHackable(DoorAnimTrigger expectedDoor)
    {
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, hackRange) && hit.collider.CompareTag("Door"))
        {
            DoorAnimTrigger door = hit.collider.GetComponentInParent<DoorAnimTrigger>();
            if (door == null)
                door = hit.collider.GetComponent<DoorAnimTrigger>();

            if (door == null)
                return false;

            if (door != expectedDoor)
                return false;

            if (!door.HasPuzzle())
                return false;

            if (!door.IsLocked())
                return false;

            if (IsDoorOnCooldown(door))
                return false;

            return true;
        }

        return false;
    }

    public bool IsDoorOnCooldown(DoorAnimTrigger door)
    {
        if (door == null)
            return false;

        if (!doorCooldownEndTime.TryGetValue(door, out float endTime))
            return false;

        if (Time.time >= endTime)
        {
            doorCooldownEndTime.Remove(door);
            return false;
        }

        return true;
    }

    public float GetDoorCooldownRemaining(DoorAnimTrigger door)
    {
        if (!IsDoorOnCooldown(door))
            return 0f;

        return Mathf.Max(0f, doorCooldownEndTime[door] - Time.time);
    }

    public bool IsLastLookedAtDoorOnCooldown()
    {
        return lastDoorLookedAt != null && IsDoorOnCooldown(lastDoorLookedAt);
    }

    public float GetLastLookedAtDoorCooldownRemaining()
    {
        return lastDoorLookedAt != null ? GetDoorCooldownRemaining(lastDoorLookedAt) : 0f;
    }

    public float GetLastLookedAtDoorCooldownProgress01()
    {
        if (lastDoorLookedAt == null)
            return 0f;

        if (!IsDoorOnCooldown(lastDoorLookedAt))
            return 0f;

        float remaining = GetDoorCooldownRemaining(lastDoorLookedAt);
        if (hackCooldownSeconds <= 0f)
            return 1f;

        // Fill bar while cooling down (0 -> 1).
        return Mathf.Clamp01(1f - (remaining / hackCooldownSeconds));
    }
    
    public bool CanHack()
    {
        lastDoorLookedAt = null;

        if (isHacking) 
        {
            return false;
        }
        
        // Raycast to check if player is looking at a door
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, hackRange))
        {
            if (hit.collider.CompareTag("Door"))
            {
                // Try to find DoorAnimTrigger on the parent or the object itself
                DoorAnimTrigger door = hit.collider.GetComponentInParent<DoorAnimTrigger>();
                if (door == null)
                {
                    door = hit.collider.GetComponent<DoorAnimTrigger>();
                }
                
                if (door != null)
                {
                    // Always set lastDoorLookedAt for doors with puzzles (for cooldown display)
                    if (door.HasPuzzle())
                    {
                        lastDoorLookedAt = door;
                    }

                    if (door.HasPuzzle() && door.IsLocked())
                    {
                        if (IsDoorOnCooldown(door))
                        {
                            return false;
                        }

                        targetDoor = door;
                        return true;
                    }
                }
            }
        }
        return false;
    }
    
    public void StartHack()
    {
        if (CanHack() && !isHoldingHack && !isHacking)
        {
            isHoldingHack = true;
            hackHoldTimer = 0f;
            Debug.Log("Starting hack hold...");
        }
    }
    
    private void CompleteHackHold()
    {
        if (!isHoldingHack)
            return;
            
        isHoldingHack = false;
        isHacking = true;
        hackHoldTimer = 0f;

        if (passwordUi != null)
            passwordUi.HideImmediately();

        if (sequenceInput != null)
            sequenceInput.SetUI(passwordUi);

        if (playerMovement != null)
            playerMovement.enabled = false;

        StartPasswordMinigame();
        Debug.Log("Hack hold complete, starting password minigame...");
    }
    
    private void CancelHackHold()
    {
        isHoldingHack = false;
        hackHoldTimer = 0f;
        targetDoor = null;
        Debug.Log("Hack hold cancelled");
    }

    private void StartPasswordMinigame()
    {
        if (passwordUi == null || sequenceInput == null || targetDoor == null)
        {
            CancelHack();
            return;
        }

        System.Collections.Generic.List<PasswordNode.Direction> seq = GenerateRandomSequence(passwordLength);
        passwordUi.ShowSequence(seq, passwordDisplaySeconds, null);
        sequenceInput.StartListening(seq, OnPasswordFinished);
    }

    private System.Collections.Generic.List<PasswordNode.Direction> GenerateRandomSequence(int length)
    {
        var sequence = new System.Collections.Generic.List<PasswordNode.Direction>(length);
        var random = new System.Random();
        for (int i = 0; i < length; i++)
            sequence.Add((PasswordNode.Direction)random.Next(0, 4));
        return sequence;
    }

    private void OnPasswordFinished(bool success)
    {
        if (!isHacking)
            return;

        if (success)
        {
            if (targetDoor != null)
                targetDoor.UnlockDoor();

            CancelHack();
            return;
        }

        // On failure: disable screen and set cooldown
        if (targetDoor != null)
            doorCooldownEndTime[targetDoor] = Time.time + hackCooldownSeconds;

        CancelHack();
    }
    
    public void CancelHack()
    {
        isHoldingHack = false;
        isHacking = false;
        targetDoor = null;
        hackHoldTimer = 0f;

        if (sequenceInput != null)
            sequenceInput.StopListeningExternal(false);

        if (passwordUi != null)
            passwordUi.HideImmediately();

        if (playerMovement != null)
            playerMovement.enabled = true;
    }
    
    public bool IsHacking()
    {
        return isHacking;
    }
    
    public float GetHackProgress()
    {
        if (isHoldingHack && hackHoldDuration > 0f)
            return Mathf.Clamp01(hackHoldTimer / hackHoldDuration);
            
        if (isHacking)
            return 1f;

        if (IsLastLookedAtDoorOnCooldown())
            return GetLastLookedAtDoorCooldownProgress01();

        return 0f;
    }
    
    public bool IsHoldingHack()
    {
        return isHoldingHack;
    }
    
    public float GetHackHoldProgress()
    {
        if (isHoldingHack && hackHoldDuration > 0f)
            return Mathf.Clamp01(hackHoldTimer / hackHoldDuration);
        return 0f;
    }
}
