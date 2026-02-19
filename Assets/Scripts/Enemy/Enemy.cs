using UnityEngine;
using System.Collections;
using UnityEngine.AI;
using System.Linq;
using System.Collections.Generic;

public class Enemy : MonoBehaviour, IDamageable
{
    public int health = 100;

    public GameObject bulletPrefab;
    public Transform[] bulletSpawnPoint;
    public GameObject muzzleFlash;
    public float bloom;
    public float fireRate;
    private float lastshotTime;

    public Material hitMat;

    public AudioClip shootingSFX;
    
    [Header("Visual Effects")]
    public GameObject visualEffectsObject; // Assign the model part with renderer in inspector

    private Rigidbody rb;
    private Renderer rend;
    private Material originalMaterial;

    private NavMeshAgent agent;

    //AI Settings
    public int currentPointIndex = 0;
    public int previousPointIndex = -1;
    public Vector3 currentTarget;
    public float positionThreshold;
    public float idleTime = 5f;
    public float attackDistance = 5f;
    public float maxVisionDistance = 20f;
    public float minChasingHealth = 30f;

    public Transform[] patrolPoints;
    private float idleTimeCounter;
    private Transform PlayerTransform;
    private bool canSeePlayer;
    private Vector3 lastKnownPlayerPosition;
    private Vector3 lastSeenDirection;
    private bool isPursuingBeyond = false;
    private Vector3 pursuitTarget;
    
    // Player caching for performance
    private List<FPSMovement> activePlayers = new List<FPSMovement>();
    private float playerUpdateTimer = 0f;
    private float playerUpdateInterval = 1f;

    // Lose sight timer for state transitions
    private float loseSightTimer = 0f;
    private float loseSightThreshold = 15f;

    public enum EnemyState { Idle, Patrolling, Chasing, Attacking }
    public EnemyState enemyState = EnemyState.Idle;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // Get renderer from the assigned visual effects object
        if (visualEffectsObject != null)
        {
            rend = visualEffectsObject.GetComponent<Renderer>();
            if (rend != null)
            {
                originalMaterial = rend.material;
            }
            else
            {
                Debug.LogWarning($"[Enemy] No Renderer found on assigned visualEffectsObject: {visualEffectsObject.name}");
            }
        }
        else
        {
            Debug.LogWarning("[Enemy] No visualEffectsObject assigned in inspector. Hit effects will be disabled.");
        }

        agent = GetComponent<NavMeshAgent>();
        UpdateActivePlayers();
        PlayerTransform = FindClosestPlayer();

        // Find patrol points only within the current room
        FindPatrolPointsInCurrentRoom();
    }
    
    private void UpdateActivePlayers()
    {
        activePlayers = FindObjectsOfType<FPSMovement>().ToList();
    }

    private Transform FindClosestPlayer()
    {
        Transform closestPlayer = null;
        
        // Use cached active players
        FPSMovement[] fpsMovements = activePlayers.ToArray();
        if (fpsMovements.Length > 0)
        {
            GameObject[] players = new GameObject[fpsMovements.Length];
            for (int i = 0; i < fpsMovements.Length; i++)
            {
                players[i] = fpsMovements[i].gameObject;
            }
            closestPlayer = GetClosestPlayerFromList(players);
        }
        
        // If no valid (non-downed) players found, clear the target
        if (closestPlayer == null)
        {
            Debug.Log("[Enemy] No valid players found (no FPSMovement components found)");
        }
        
        return closestPlayer;
    }
    
    private Transform GetClosestPlayerFromList(GameObject[] players)
    {
        Transform closestPlayer = null;
        float closestDistance = float.MaxValue;
        
        foreach (GameObject player in players)
        {
            if (player != null && !IsPlayerDowned(player.transform))
            {
                float distance = Vector3.Distance(transform.position, player.transform.position);
                
                // Only consider players within vision distance
                if (distance <= maxVisionDistance)
                {
                    // Check if we have line of sight to this player
                    Vector3 directionToPlayer = player.transform.position - transform.position;
                    bool hasLineOfSight = Physics.Raycast(transform.position, directionToPlayer.normalized, out RaycastHit hit, maxVisionDistance);
                    bool canSeeThisPlayer = hasLineOfSight && hit.transform == player.transform;
                    
                    // Only consider players we can see, or if no one is visible yet
                    if (canSeeThisPlayer || closestPlayer == null)
                    {
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestPlayer = player.transform;
                        }
                    }
                }
            }
        }
        
        return closestPlayer;
    }

    private void FindPatrolPointsInCurrentRoom()
    {
        // Find the GameObject tagged "PatrolPoint" that is a child of the room the enemy spawned in
        RoomGen currentRoom = GetComponentInParent<RoomGen>();
        if (currentRoom != null)
        {
            // Search for the PatrolPoint tagged GameObject within this room's children
            Transform[] roomChildren = currentRoom.GetComponentsInChildren<Transform>();
            GameObject patrolPointParent = null;
            
            foreach (Transform child in roomChildren)
            {
                if (child.gameObject.CompareTag("PatrolPoint"))
                {
                    patrolPointParent = child.gameObject;
                    break;
                }
            }
            
            if (patrolPointParent != null)
            {
                // Get all children of the PatrolPoint GameObject as patrol points
                Transform[] childTransforms = patrolPointParent.GetComponentsInChildren<Transform>();
                List<Transform> validPatrolPoints = new List<Transform>();
                
                foreach (Transform child in childTransforms)
                {
                    // Exclude the parent object itself, only include the children
                    if (child != patrolPointParent.transform)
                    {
                        validPatrolPoints.Add(child);
                    }
                }
                
                patrolPoints = validPatrolPoints.ToArray();
                Debug.Log($"[Enemy] Found {patrolPoints.Length} patrol points from '{patrolPointParent.name}' in current room");
                
                if (patrolPoints.Length > 0)
                {
                    currentTarget = patrolPoints[0].position;
                    return;
                }
            }
            else
            {
                Debug.LogWarning("[Enemy] No GameObject with 'PatrolPoint' tag found in current room");
            }
        }
        else
        {
            Debug.LogWarning("[Enemy] Enemy is not a child of a RoomGen object");
        }
        
        // Fallback: Get patrol points from ALL rooms for corridor navigation
        Debug.Log("[Enemy] Getting patrol points from all rooms for corridor navigation");
        var allPatrolPoints = new List<Transform>();
        
        RoomGen[] allRooms = FindObjectsOfType<RoomGen>();
        foreach (RoomGen room in allRooms)
        {
            // Search for PatrolPoint tagged GameObjects in each room
            Transform[] roomChildren = room.GetComponentsInChildren<Transform>();
            GameObject patrolPointParent = null;
            
            foreach (Transform child in roomChildren)
            {
                if (child.gameObject.CompareTag("PatrolPoint"))
                {
                    patrolPointParent = child.gameObject;
                    break;
                }
            }
            
            if (patrolPointParent != null)
            {
                Transform[] childTransforms = patrolPointParent.GetComponentsInChildren<Transform>();
                foreach (Transform child in childTransforms)
                {
                    if (child != patrolPointParent.transform)
                    {
                        allPatrolPoints.Add(child);
                    }
                }
            }
        }
        
        patrolPoints = allPatrolPoints.ToArray();
        Debug.Log($"[Enemy] Found {patrolPoints.Length} total patrol points from all rooms");
        
        if (patrolPoints.Length > 0)
        {
            currentTarget = patrolPoints[0].position;
        }
        else
        {
            Debug.LogWarning("[Enemy] No patrol points found anywhere");
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Damage")
        {
            TakeDamage(10);
        }
    }

    public void TakeDamage(int amount)
    {
        health -= amount;
        if (health <= 0)
        {
            Die();
        }
        else
        {
            StartCoroutine(Blink());
        }
    }

    void Die()
    {

        //Destroy(gameObject);

        if (!this.enabled) return;

        rb.freezeRotation = false;
        transform.rotation = Quaternion.Euler(transform.rotation.x, transform.rotation.y, transform.rotation.z + 5);
        agent.enabled = false; // Disable NavMeshAgent to stop movement
        if (GetComponent<Fly>() != null)
        {
            GetComponent<Fly>().enabled = false; // Disable Fly script to stop hovering
        }
        
        this.enabled = false; // Disable enemy behavior
    }

    IEnumerator Blink()
    {
        if (rend != null && hitMat != null)
        {
            rend.material = hitMat;
            yield return new WaitForSeconds(0.1f);
            if (rend != null && originalMaterial != null)
            {
                rend.material = originalMaterial;
            }
        }
        else
        {
            // If no renderer available, just wait for the duration
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void Update()
    {
        // Update player cache periodically
        playerUpdateTimer += Time.deltaTime;
        if (playerUpdateTimer >= playerUpdateInterval)
        {
            UpdateActivePlayers();
            playerUpdateTimer = 0f;
        }
        
        LookForPlayer();

        switch (enemyState)
        {
            case EnemyState.Idle:
                IdleBehavior();
                break;
            case EnemyState.Patrolling:
                PatrolBehavior();
                break;
            case EnemyState.Attacking:
                AttackBehavior();
                break;
            case EnemyState.Chasing:
                ChaseBehavior();
                break;
        }

        LookAtPlayer();
        SetLastKnownPlayerPosition();
    }

    private void LookForPlayer()
    {
        if (!EnsurePlayerExists()) return;

        if (PlayerTransform == null) return; // Additional safety check

        Vector3 directionToPlayer = PlayerTransform.position - transform.position;
        bool hasLineOfSight = Physics.Raycast(transform.position, directionToPlayer.normalized, out RaycastHit hit, maxVisionDistance);
        
        canSeePlayer = hasLineOfSight && hit.transform == PlayerTransform;

        // Update lose sight timer
        if (canSeePlayer)
        {
            loseSightTimer = 0f;
        }
        else
        {
            loseSightTimer += Time.deltaTime;
        }

        if (canSeePlayer && enemyState != EnemyState.Attacking)
        {
            enemyState = EnemyState.Chasing;
        }
    }

    private bool EnsurePlayerExists()
    {
        // Check if current target is still valid
        if (PlayerTransform != null && !IsPlayerDowned(PlayerTransform) && activePlayers.Contains(PlayerTransform.GetComponent<FPSMovement>()))
        {
            return true;
        }

        // Find closest valid player
        Transform closestPlayer = FindClosestPlayer();
        if (closestPlayer != null && closestPlayer != PlayerTransform)
        {
            PlayerTransform = closestPlayer;
            Debug.Log($"[Enemy] Targeting player: {PlayerTransform.name}");
        }
        else if (closestPlayer == null)
        {
            PlayerTransform = null;
            Debug.Log("[Enemy] No valid players found");
        }

        return PlayerTransform != null;
    }
    
    private bool IsPlayerDowned(Transform playerTransform)
    {
        if (playerTransform == null) return false;
        
        PlayerHealth playerHealth = playerTransform.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            return playerHealth.IsDowned();
        }
        
        return false;
    }
    

    private void IdleBehavior()
    {
        agent.ResetPath();
        idleTimeCounter -= Time.deltaTime;
        if (idleTimeCounter <= 0)
        {
            idleTimeCounter = idleTime;
            enemyState = EnemyState.Patrolling;
        }

    }

    private void PatrolBehavior()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            // No patrol points available, try to find them again or go idle
            FindPatrolPointsInCurrentRoom();
            
            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                // Still no patrol points, just idle for a bit then try again
                enemyState = EnemyState.Idle;
                return;
            }
        }
        
        // Initialize currentTarget if it's not set
        if (currentTarget == Vector3.zero && patrolPoints.Length > 0)
        {
            currentTarget = patrolPoints[0].position;
        }
        
        if (Vector3.Distance(currentTarget, transform.position) < positionThreshold)
        {
            canSeePlayer = false;
            float chance = Random.Range(0, 100);
            if (chance < 10)// 10% chance to idle at the patrol point
            {
                enemyState = EnemyState.Idle;
                return;
            }
            
            // Select next patrol point randomly, avoiding the immediately previous point
            SelectNextPatrolPoint();
        }
        else
        {
            agent.SetDestination(currentTarget);
        }
    }
    
    private void SelectNextPatrolPoint()
    {
        if (patrolPoints.Length <= 1)
        {
            // If there's only one patrol point, just use it
            currentPointIndex = 0;
            currentTarget = patrolPoints[0].position;
            return;
        }
        
        // Create a list of available patrol points (excluding the previous point)
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (i != previousPointIndex)
            {
                availableIndices.Add(i);
            }
        }
        
        // Randomly select from available indices
        int nextIndex = availableIndices[Random.Range(0, availableIndices.Count)];
        
        // Update indices and target
        previousPointIndex = currentPointIndex;
        currentPointIndex = nextIndex;
        currentTarget = patrolPoints[currentPointIndex].position;
        
        Debug.Log($"[Enemy] Selected patrol point {currentPointIndex}, avoiding previous point {previousPointIndex}");
    }

    private void ChaseBehavior()
    {
        idleTimeCounter = idleTime; // Reset idle timer when switching to chasing
        
        if (loseSightTimer > loseSightThreshold)
        {
            // Lost interest, return to patrolling
            enemyState = EnemyState.Patrolling;
            PlayerTransform = null;
            agent.ResetPath();
            isPursuingBeyond = false;
            return;
        }
        
        if (health < minChasingHealth)
        {
            enemyState = EnemyState.Patrolling; //cautious
            PlayerTransform = null;
            isPursuingBeyond = false;
        }
        else if (PlayerTransform != null && Vector3.Distance(transform.position, PlayerTransform.position) <= attackDistance && canSeePlayer)
        {
            enemyState = EnemyState.Attacking;
        }
        else
        {
            // Determine destination
            if (PlayerTransform != null && canSeePlayer && Vector3.Distance(transform.position, PlayerTransform.position) <= maxVisionDistance)
            {
                agent.SetDestination(PlayerTransform.position);
            }
            else if (isPursuingBeyond)
            {
                agent.SetDestination(pursuitTarget);
                if (Vector3.Distance(transform.position, pursuitTarget) < positionThreshold)
                {
                    isPursuingBeyond = false;
                    enemyState = EnemyState.Patrolling;
                }
            }
            else if (Vector3.Distance(transform.position, lastKnownPlayerPosition) < positionThreshold)
            {
                // Reached last known, start pursuing beyond
                pursuitTarget = lastKnownPlayerPosition + lastSeenDirection * 10f;
                agent.SetDestination(pursuitTarget);
                isPursuingBeyond = true;
            }
            else
            {
                agent.SetDestination(lastKnownPlayerPosition);
            }
        }
    }

    private void AttackBehavior()
    {
        idleTimeCounter = idleTime; // Reset idle timer when attacking
        agent.ResetPath();

        Shoot();

        if (PlayerTransform == null || Vector3.Distance(transform.position, PlayerTransform.position) > attackDistance || !canSeePlayer)
        {
            if(health < minChasingHealth)
            {
                enemyState = EnemyState.Patrolling; //cautious
                PlayerTransform = null;
            }
            else
            {
                enemyState = EnemyState.Chasing;
            }
        }
    }

    private void LookAtPlayer()
    {
        if (!canSeePlayer || !EnsurePlayerExists()) return;
        
        if (PlayerTransform == null) return; // Additional safety check
        
        Vector3 lookPosition = PlayerTransform.position;
        lookPosition.y = transform.position.y; // Keep enemy upright
        transform.LookAt(lookPosition);
    }

    private void SetLastKnownPlayerPosition()
    {
        if (canSeePlayer && EnsurePlayerExists())
        {
            if (PlayerTransform == null) return; // Additional safety check
            lastKnownPlayerPosition = PlayerTransform.position;
            lastSeenDirection = (PlayerTransform.position - transform.position).normalized;
        }
    }

    private void Shoot()
    {
        if(Time.time > lastshotTime + fireRate)
        {
            // Check for required components
            if (PlayerTransform == null || bulletSpawnPoint == null || bulletSpawnPoint.Length == 0)
            {
                Debug.LogWarning("[Enemy] Missing PlayerTransform or bulletSpawnPoint");
                return;
            }
            
            // Shoot from each bullet spawn point (for dual-wielding enemies)
            foreach (Transform spawnPoint in bulletSpawnPoint)
            {
                if (spawnPoint == null) continue;
                
                Vector3 shootDirection = (PlayerTransform.position - spawnPoint.position).normalized;
                shootDirection.Normalize();

                Quaternion bulletRotation = Quaternion.LookRotation(shootDirection);

                float maxInaccuracy = 10f;
                float currentInaccuracy = bloom * maxInaccuracy;
                float randomYaw = Random.Range(-currentInaccuracy, currentInaccuracy);
                float randomPitch = Random.Range(-currentInaccuracy, currentInaccuracy);

                bulletRotation *= Quaternion.Euler(randomPitch, randomYaw, 0f);

                // Instantiate bullet and muzzle flash if prefabs are available
                if (bulletPrefab != null)
                {
                    Instantiate(bulletPrefab, spawnPoint.position, bulletRotation);
                }
                
                if (muzzleFlash != null)
                {
                    Instantiate(muzzleFlash, spawnPoint.position, bulletRotation);
                }
            }

            // Play shooting sound if AudioManager and audio clip are available
            if (AudioManager.Instance != null && shootingSFX != null)
            {
                AudioManager.Instance.PlaySFX(shootingSFX, 0.25f);
            }

            lastshotTime = Time.time;
        }
    }
}
