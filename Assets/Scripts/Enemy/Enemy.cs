using UnityEngine;
using System.Collections;
using UnityEngine.AI;
using System.Linq;

public class Enemy : MonoBehaviour
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
        PlayerTransform = GameObject.FindWithTag("Player").GetComponent<Transform>();

        GameObject patrolPointParent = GameObject.FindWithTag("PatrolPoint");
        patrolPoints = patrolPointParent.GetComponentsInChildren<Transform>().Where(t => t != patrolPointParent.transform).ToArray();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Damage")
        {
            health -= 10;
            if (health <= 0)
            {
                Die();
            }
            else
            {
                StartCoroutine(Blink());
            }
        }
    }

    void Die()
    {

        Destroy(gameObject);

        //if (!this.enabled) return;

        //rb.freezeRotation = false;
        //transform.rotation = Quaternion.Euler(transform.rotation.x, transform.rotation.y, transform.rotation.z + 5);
        //this.enabled = false; // Disable enemy behavior
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
        Vector3 directionToPlayer = PlayerTransform.position - transform.position;
        if (Physics.Raycast(transform.position, directionToPlayer.normalized, out RaycastHit hit, maxVisionDistance))
        {
            canSeePlayer = hit.transform == PlayerTransform;

            if (canSeePlayer && enemyState != EnemyState.Attacking)
            {
                enemyState = EnemyState.Chasing;
            }
        }
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
        if (Vector3.Distance(currentTarget, transform.position) < positionThreshold)
        {
            float chance = Random.Range(0, 100);
            if (chance < 10)// 10% chance to idle at the patrol point
            {
                enemyState = EnemyState.Idle;
                return;
            }
            currentPointIndex++;
            currentTarget = patrolPoints[currentPointIndex % patrolPoints.Length].position;
        }
        else
        {
            agent.SetDestination(currentTarget);
        }
    }

    private void ChaseBehavior()
    {
        idleTimeCounter = idleTime; // Reset idle timer when switching to chasing
        agent.SetDestination(lastKnownPlayerPosition);
        if(health < minChasingHealth)
        {
            enemyState = EnemyState.Patrolling; //cautious
        }
        else if (Vector3.Distance(transform.position, PlayerTransform.position) <= attackDistance && canSeePlayer)
        {
            enemyState = EnemyState.Attacking;
        }
        else if (Vector3.Distance(transform.position, PlayerTransform.position) > maxVisionDistance)
        {
            enemyState = EnemyState.Patrolling; // Lost sight of player, return to patrolling
        }
        else if (Vector3.Distance(transform.position, PlayerTransform.position) < positionThreshold && !canSeePlayer)
        {
            enemyState = EnemyState.Patrolling;
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
        if (canSeePlayer)
        {
            transform.LookAt(new Vector3(PlayerTransform.position.x, transform.position.y, PlayerTransform.position.z));
        }
    }

    private void SetLastKnownPlayerPosition()
    {
        if (canSeePlayer)
        {
            lastKnownPlayerPosition = PlayerTransform.position;
        }
    }

    private void Shoot()
    {
        if(Time.time > lastshotTime + fireRate)
        {
            Vector3 shootDirection = (PlayerTransform.position - bulletSpawnPoint.position).normalized;
            shootDirection.Normalize();

            Quaternion bulletRotation = Quaternion.LookRotation(shootDirection);

            float maxInaccuracy = 10f;
            float currentInaccuracy = bloom * maxInaccuracy;
            float randomYaw = Random.Range(-currentInaccuracy, currentInaccuracy);
            float randomPitch = Random.Range(-currentInaccuracy, currentInaccuracy);

            bulletRotation *= Quaternion.Euler(randomPitch, randomYaw, 0f);

            AudioManager.Instance.PlaySFX(shootingSFX, 0.25f);

            Instantiate(bulletPrefab, bulletSpawnPoint.position, bulletRotation);
            Instantiate(muzzleFlash, bulletSpawnPoint.position, bulletRotation);
            lastshotTime = Time.time;
        }
    }
}
