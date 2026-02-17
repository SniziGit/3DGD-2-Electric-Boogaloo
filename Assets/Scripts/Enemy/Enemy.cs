using UnityEngine;
using System.Collections;
using UnityEngine.AI;
using System.Linq;
using System.Collections.Generic;

public class Enemy : MonoBehaviour, IDamageable
{
    public int health = 100;

    public GameObject bulletPrefab;
    public Transform bulletSpawnPoint;
    public GameObject muzzleFlash;
    public float bloom;
    public float fireRate;
    private float lastshotTime;

    public Material hitMat;

    public AudioClip shootingSFX;

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

    public enum EnemyState { Idle, Patrolling, Chasing, Attacking }
    public EnemyState enemyState = EnemyState.Idle;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rend = GetComponent<Renderer>();
        originalMaterial = rend.material;

        agent = GetComponent<NavMeshAgent>();
        FindPlayerTransform();

        // Find patrol points only within the current room
        FindPatrolPointsInCurrentRoom();
    }
    
    private void FindPlayerTransform()
    {
        if (PlayerTransform != null) return; // Already found
        
        // Try multiple strategies to find the player
        PlayerTransform = FindPlayerByTag() ?? FindPlayerBySpawner() ?? null;
        
        if (PlayerTransform != null)
        {
            Debug.Log($"[Enemy] Found player: {PlayerTransform.name}");
        }
    }

    private Transform FindPlayerByTag()
    {
        GameObject player = GameObject.FindWithTag("Player");
        return player?.transform;
    }

    private Transform FindPlayerBySpawner()
    {
        PlayerSpawning playerSpawning = FindObjectOfType<PlayerSpawning>();
        return playerSpawning?.GetSpawnedPlayer()?.transform;
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
        rend.material = hitMat;
        yield return new WaitForSeconds(0.1f);
        rend.material = originalMaterial;
    }

    private void Update()
    {
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

        rb.linearVelocity = Vector3.zero;

        LookAtPlayer();
        SetLastKnownPlayerPosition();
    }

    private void LookForPlayer()
    {
        if (!EnsurePlayerExists()) return;

        Vector3 directionToPlayer = PlayerTransform.position - transform.position;
        bool hasLineOfSight = Physics.Raycast(transform.position, directionToPlayer.normalized, out RaycastHit hit, maxVisionDistance);
        
        canSeePlayer = hasLineOfSight && hit.transform == PlayerTransform;

        if (canSeePlayer && enemyState != EnemyState.Attacking)
        {
            enemyState = EnemyState.Chasing;
        }
    }

    private bool EnsurePlayerExists()
    {
        if (PlayerTransform != null) return true;
        
        FindPlayerTransform();
        return PlayerTransform != null;
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
        
        // Check if enemy has reached the last known player position
        float distanceToLastKnown = Vector3.Distance(transform.position, lastKnownPlayerPosition);
        
        if(health < minChasingHealth)
        {
            enemyState = EnemyState.Patrolling; //cautious
        }
        else if (Vector3.Distance(transform.position, PlayerTransform.position) <= attackDistance && canSeePlayer)
        {
            enemyState = EnemyState.Attacking;
        }
        else if (canSeePlayer && Vector3.Distance(transform.position, PlayerTransform.position) <= maxVisionDistance)
        {
            // Still can see player, continue chasing
            agent.SetDestination(PlayerTransform.position);
        }
        else if (distanceToLastKnown < positionThreshold)
        {
            // Reached last known position but player not found, return to patrol
            enemyState = EnemyState.Patrolling;
        }
        else
        {
            // Continue to last known position
            agent.SetDestination(lastKnownPlayerPosition);
        }
    }

    private void AttackBehavior()
    {
        idleTimeCounter = idleTime; // Reset idle timer when attacking
        agent.ResetPath();

        Shoot();

        if (Vector3.Distance(transform.position, PlayerTransform.position) > attackDistance || !canSeePlayer)
        {
            if(health < minChasingHealth)
            {
                enemyState = EnemyState.Patrolling; //cautious
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
        
        Vector3 lookPosition = PlayerTransform.position;
        lookPosition.y = transform.position.y; // Keep enemy upright
        transform.LookAt(lookPosition);
    }

    private void SetLastKnownPlayerPosition()
    {
        if (canSeePlayer && EnsurePlayerExists())
        {
            lastKnownPlayerPosition = PlayerTransform.position;
        }
    }

    private void Shoot()
    {
        if(Time.time > lastshotTime + fireRate)
        {
            // Check for required components
            if (PlayerTransform == null || bulletSpawnPoint == null)
            {
                Debug.LogWarning("[Enemy] Missing PlayerTransform or bulletSpawnPoint");
                return;
            }
            
            Vector3 shootDirection = (PlayerTransform.position - bulletSpawnPoint.position).normalized;
            shootDirection.Normalize();

            Quaternion bulletRotation = Quaternion.LookRotation(shootDirection);

            float maxInaccuracy = 10f;
            float currentInaccuracy = bloom * maxInaccuracy;
            float randomYaw = Random.Range(-currentInaccuracy, currentInaccuracy);
            float randomPitch = Random.Range(-currentInaccuracy, currentInaccuracy);

            bulletRotation *= Quaternion.Euler(randomPitch, randomYaw, 0f);

            // Play shooting sound if AudioManager and audio clip are available
            if (AudioManager.Instance != null && shootingSFX != null)
            {
                AudioManager.Instance.PlaySFX(shootingSFX, 0.25f);
            }

            // Instantiate bullet and muzzle flash if prefabs are available
            if (bulletPrefab != null)
            {
                Instantiate(bulletPrefab, bulletSpawnPoint.position, bulletRotation);
            }
            
            if (muzzleFlash != null)
            {
                Instantiate(muzzleFlash, bulletSpawnPoint.position, bulletRotation);
            }

            lastshotTime = Time.time;
        }
    }
}
