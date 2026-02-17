using UnityEngine;

public class ReviveInteraction : MonoBehaviour
{
    [Header("Revive Settings")]
    [SerializeField] private float reviveTime = 3f;
    [SerializeField] private int reviveHealth = 50;
    [SerializeField] private float reviveRange = 2f;
    
    private bool isReviving = false;
    private float reviveTimer = 0f;
    private PlayerHealth targetPlayer;
    private FPSMovement reviverMovement;
    
    void Start()
    {
        reviverMovement = GetComponent<FPSMovement>();
    }
    
    void Update()
    {
        if (isReviving && targetPlayer != null)
        {
            reviveTimer += Time.deltaTime;
            
            // Check if target is still in range and still downed
            if (Vector3.Distance(transform.position, targetPlayer.transform.position) > reviveRange || !targetPlayer.IsDowned())
            {
                CancelRevive();
                return;
            }
            
            // Complete revive
            if (reviveTimer >= reviveTime)
            {
                CompleteRevive();
            }
        }
    }
    
    public bool CanRevive()
    {
        if (isReviving) return false;
        
        // Find all players with "Player" tag
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        
        foreach (GameObject player in players)
        {
            if (player != gameObject) // Don't check self
            {
                PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
                if (playerHealth != null && playerHealth.IsDowned())
                {
                    float distance = Vector3.Distance(transform.position, player.transform.position);
                    if (distance <= reviveRange)
                    {
                        targetPlayer = playerHealth;
                        return true;
                    }
                }
            }
        }
        return false;
    }
    
    public void StartRevive()
    {
        if (CanRevive() && !isReviving)
        {
            isReviving = true;
            reviveTimer = 0f;
            Debug.Log("Started reviving player...");
        }
    }
    
    private void CompleteRevive()
    {
        if (targetPlayer != null)
        {
            targetPlayer.Revive(reviveHealth);
            Debug.Log($"Revived player with {reviveHealth} health");
        }
        
        CancelRevive();
    }
    
    private void CancelRevive()
    {
        isReviving = false;
        reviveTimer = 0f;
        targetPlayer = null;
    }
    
    public float GetReviveProgress()
    {
        return isReviving ? reviveTimer / reviveTime : 0f;
    }
    
    public bool IsReviving()
    {
        return isReviving;
    }
}
