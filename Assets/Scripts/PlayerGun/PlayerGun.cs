using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.LowLevel.InputStateHistory;

public interface IDamageable
{
    void TakeDamage(int amount);
}

public class PlayerGun : MonoBehaviour
{
    [Header("Shooting Settings")]
    public float fireRate = 0.1f;
    public float range = 100f;
    public int damage = 25;
    public int magSize = 30;
    public float reloadTime = 2f;
    
    [Header("Crosshair Recoil")]
    public GameObject recoilCrosshair;
    public GameObject persistentRecoilObject;
    public GameObject hitCrosshair;
    public float recoilAmount = 5f;
    public float recoilDuration = 0.2f;
    public float recoilRecoverySpeed = 5f;
    
    [Header("Effects")]
    public Camera playerCamera;
    public LayerMask shootableLayers;
    public LayerMask wallLayers; // For walls that block enemy detection
    public GameObject weaponFlash;
    public GameObject weaponParticles;
    public Transform flashSpawnPoint;
    
    [Header("Ammo UI")]
    public UnityEngine.UI.Image ammoFillImage;
    public float fillSmoothSpeed = 5f;

    // Crosshair recoil variables
    private float nextTimeToFire;
    private Vector3 originalCrosshairPosition;
    private Vector3 currentRecoilOffset;
    private Vector3 originalPersistentPosition;
    private Vector3 currentPersistentOffset;
    private Vector3 originalHitCrosshairPosition;
    private Vector3 currentHitRecoilOffset;
    private bool isHoldingShoot = false;
    private bool showingHitCrosshair = false;
    private UnityEngine.UI.Image persistentImage;
    private Color originalPersistentColor;

    // Physical+Crosshair recoil variables
    private int currentAmmo;
    private bool isReloading = false;
    private float recoilTimer;
    private float currentFillAmount;
    private float targetFillAmount;

    // Physical recoil variables
    public float recoilDistance = 0.1f;
    public float recoilSpeed = 15f;

    private Quaternion initialRotation;
    private Vector3 initialPosition;
    private Vector3 reloadRotationOffset = new Vector3(66, 55, 55);

    public AudioClip shootingSFX;

    void Start()
    {
        currentAmmo = magSize;
        initialRotation = transform.localRotation;
        initialPosition = transform.localPosition;
        
        // Initialize ammo fill
        currentFillAmount = 1f; // 100% = full mag
        targetFillAmount = 1f;
        
        if (ammoFillImage != null)
        {
            ammoFillImage.fillAmount = currentFillAmount;
        }
        
        // Initialize crosshair positions
        if (recoilCrosshair != null)
        {
            originalCrosshairPosition = recoilCrosshair.transform.localPosition;
            recoilCrosshair.SetActive(false); // Start hidden
        }
        
        if (persistentRecoilObject != null)
        {
            originalPersistentPosition = persistentRecoilObject.transform.localPosition;
            persistentImage = persistentRecoilObject.GetComponent<UnityEngine.UI.Image>();
            if (persistentImage != null)
                originalPersistentColor = persistentImage.color;
        }
        
        if (hitCrosshair != null)
        {
            originalHitCrosshairPosition = hitCrosshair.transform.localPosition;
            hitCrosshair.SetActive(false); // Start hidden
        }
    }

    void Update()
    {
        HandleShooting();
        UpdateCrosshairRecoil();
        UpdatePersistentRecoil();
        UpdateHitCrosshair();
        UpdateAmmoFill();
        CheckEnemyAim();
    }
   
    
    // Methods expected by PlayerShooting.cs
    public void Shoot()
    {
        if (isReloading) return;
        if (Time.time < nextTimeToFire) return;
        
        if (currentAmmo <= 0)
        {
            StartCoroutine(Reload());
            return;
        }

        AudioManager.Instance.PlaySFX(shootingSFX, 0.25f);

        Instantiate(weaponFlash, flashSpawnPoint.position, flashSpawnPoint.rotation);
        Instantiate(weaponParticles, flashSpawnPoint.position, flashSpawnPoint.rotation);

        nextTimeToFire = Time.time + fireRate;
        currentAmmo--;
        UpdateAmmoFillTarget();
        ShootFromCrosshair();
        StopCoroutine(nameof(PhysicalRecoil));
        StartCoroutine(nameof(PhysicalRecoil));
    }
    
    public void Aim()
    {
        // Could add aim-specific behavior here if needed
    }
    
    public void StopAiming()
    {
        // Could add stop-aim behavior here if needed
    }
    
    public void TryReload()
    {
        if (!isReloading && currentAmmo < magSize)
        {
            StartCoroutine(Reload());
        }
    }
    
    private IEnumerator Reload()
    {
        isReloading = true;
        Debug.Log("Reloading...");

        Quaternion targetRotation = Quaternion.Euler(initialRotation.eulerAngles + reloadRotationOffset);
        float halfReload = reloadTime / 2f;
        float t = 0f;

        while (t < halfReload)
        {
            t += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(initialRotation, targetRotation, t / halfReload);
            yield return null;
        }

        t = 0f;

        while (t < halfReload)
        {
            t += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(targetRotation, initialRotation, t / halfReload);
            yield return null;

        }

        currentAmmo = magSize;
        UpdateAmmoFillTarget();
        isReloading = false;
        Debug.Log("Reload complete!");
    }
    
    void HandleShooting()
    {
        if (isHoldingShoot && !isReloading && Time.time >= nextTimeToFire)
        {
            if (currentAmmo <= 0)
            {
                StartCoroutine(Reload());
                return;
            }
            
            nextTimeToFire = Time.time + fireRate;
            currentAmmo--;
            UpdateAmmoFillTarget();
            ShootFromCrosshair();
        }
    }
    
    void ShootFromCrosshair()
    {
        // Get the viewport rect of the player's camera
        Rect viewport = playerCamera.rect;

        // Calculate the center of the camera's viewport in screen coordinates
        float viewportCenterX = (Screen.width * viewport.x) + (Screen.width * viewport.width / 2f);
        float viewportCenterY = (Screen.height * viewport.y) + (Screen.height * viewport.height / 2f);

        // Raycast from camera center (crosshair position) relative to its viewport
        Ray ray = playerCamera.ScreenPointToRay(new Vector3(viewportCenterX, viewportCenterY, 0f));
        
        // Debug: Draw the ray in Scene view
        Debug.DrawRay(ray.origin, ray.direction * range, Color.red, 2f);
        
        // First raycast to check for walls
        if (Physics.Raycast(ray, out RaycastHit wallHit, range, wallLayers))
        {
            // Wall is blocking, hit the wall instead
            Debug.Log($"Shot hit wall: {wallHit.collider.name} at distance {wallHit.distance}");
            return;
        }
        
        // Second raycast to check for enemies (ignoring walls)
        if (Physics.Raycast(ray, out RaycastHit hit, range, shootableLayers))
        {
            Debug.Log($"Raycast hit: {hit.collider.name} on layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
            
            // Apply damage to hit target
            IDamageable damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
                ShowHitCrosshair(); // Show hit crosshair when hitting enemy
                Debug.Log($"Successfully dealt {damage} damage to {hit.collider.name}");
            }
            else
            {
                Debug.Log($"Hit object {hit.collider.name} but no IDamageable component found");
            }
        }
        else
        {
            // Debug: Try raycast without layer mask to see what we're hitting
            if (Physics.Raycast(ray, out RaycastHit debugHit, range))
            {
                string hitLayerName = LayerMask.LayerToName(debugHit.collider.gameObject.layer);
                Debug.Log($"Shot missed - but hit {debugHit.collider.name} on layer '{hitLayerName}' (not in shootableLayers)");
                
                // Check if this might be an enemy
                if (debugHit.collider.GetComponent<Enemy>() != null)
                {
                    Debug.LogError($"ENEMY DETECTED BUT NOT IN SHOOTABLE LAYERS! Enemy '{debugHit.collider.name}' is on layer '{hitLayerName}'. Add this layer to shootableLayers!");
                }
            }
            else
            {
                Debug.Log("Shot missed - no target in range");
            }
        }
        
        // Apply crosshair recoil
        ApplyRecoil();
    }
    
    void ApplyRecoil()
    {
        // Calculate recoil offset once for all objects
        Vector3 recoilOffset = new Vector3(
            Random.Range(-recoilAmount, recoilAmount),
            Random.Range(-recoilAmount, recoilAmount),
            0f
        );
        
        if (recoilCrosshair != null && !showingHitCrosshair)
        {
            // Show recoil crosshair
            recoilCrosshair.SetActive(true);
            currentRecoilOffset += recoilOffset;
            
            // Reset recoil timer
            recoilTimer = recoilDuration;
        }
        
        // Apply reduced recoil to persistent object
        if (persistentRecoilObject != null)
        {
            Vector3 reducedRecoil = recoilOffset * 0.3f; // 30% of normal recoil
            currentPersistentOffset += reducedRecoil;
        }
        
        // Apply recoil to hit crosshair if it's active
        if (hitCrosshair != null && showingHitCrosshair)
        {
            Vector3 hitRecoilOffset = hitCrosshair.transform.localPosition - GetOriginalHitCrosshairPosition();
            hitRecoilOffset += recoilOffset * 0.7f; // 70% of normal recoil
            hitCrosshair.transform.localPosition = GetOriginalHitCrosshairPosition() + hitRecoilOffset;
        }
    }

    private IEnumerator PhysicalRecoil()
    {
        Vector3 recoilTarget = initialPosition + new Vector3(0, 0, -recoilDistance);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * recoilSpeed;
            transform.localPosition = Vector3.Lerp(initialPosition, recoilTarget, t);
            yield return null;
        }

        t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * recoilSpeed;
            transform.localPosition = Vector3.Lerp(recoilTarget, initialPosition, t);
            yield return null;
        }

        transform.localPosition = initialPosition;
    }

    void UpdateCrosshairRecoil()
    {
        if (recoilCrosshair != null && !showingHitCrosshair)
        {
            // Update recoil timer
            if (recoilTimer > 0)
            {
                recoilTimer -= Time.deltaTime;
                
                // Smoothly return to original position
                currentRecoilOffset = Vector3.Lerp(currentRecoilOffset, Vector3.zero, Time.deltaTime * recoilRecoverySpeed);
                recoilCrosshair.transform.localPosition = originalCrosshairPosition + currentRecoilOffset;
            }
            else
            {
                // Hide recoil crosshair when timer is done
                recoilCrosshair.SetActive(false);
                currentRecoilOffset = Vector3.zero;
            }
        }
    }
    
    void UpdatePersistentRecoil()
    {
        if (persistentRecoilObject != null)
        {
            // Smoothly return to original position
            currentPersistentOffset = Vector3.Lerp(currentPersistentOffset, Vector3.zero, Time.deltaTime * recoilRecoverySpeed * 0.5f);
            persistentRecoilObject.transform.localPosition = originalPersistentPosition + currentPersistentOffset;
        }
    }
    
    void ShowHitCrosshair()
    {
        if (hitCrosshair != null && !showingHitCrosshair)
        {
            showingHitCrosshair = true;
            hitCrosshair.SetActive(true);
            
            // Hide regular recoil crosshair when hit crosshair shows
            if (recoilCrosshair != null)
                recoilCrosshair.SetActive(false);
            
            // Start coroutine to hide hit crosshair after a short duration
            StartCoroutine(HideHitCrosshairAfterDelay(0.3f));
        }
    }
    
    IEnumerator HideHitCrosshairAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        showingHitCrosshair = false;
        if (hitCrosshair != null)
            hitCrosshair.SetActive(false);
    }
    
    void UpdateHitCrosshair()
    {
        if (hitCrosshair != null && showingHitCrosshair)
        {
            // Smoothly return hit crosshair to original position
            currentHitRecoilOffset = Vector3.Lerp(currentHitRecoilOffset, Vector3.zero, Time.deltaTime * recoilRecoverySpeed * 0.8f);
            hitCrosshair.transform.localPosition = originalHitCrosshairPosition + currentHitRecoilOffset;
        }
    }
    
    Vector3 GetOriginalHitCrosshairPosition()
    {
        return originalHitCrosshairPosition;
    }
    
    void CheckEnemyAim()
    {
        if (persistentImage == null) return;
        
        // Get the viewport rect of player's camera
        Rect viewport = playerCamera.rect;

        // Calculate center of camera's viewport in screen coordinates
        float viewportCenterX = (Screen.width * viewport.x) + (Screen.width * viewport.width / 2f);
        float viewportCenterY = (Screen.height * viewport.y) + (Screen.height * viewport.height / 2f);

        // Raycast from camera center (crosshair position) relative to its viewport
        Ray ray = playerCamera.ScreenPointToRay(new Vector3(viewportCenterX, viewportCenterY, 0f));
        
        // First raycast to check for walls
        if (Physics.Raycast(ray, out RaycastHit wallHit, range, wallLayers))
        {
            // Wall is blocking, return to original color
            persistentImage.color = originalPersistentColor;
            return;
        }
        
        // Second raycast to check for enemies (ignoring walls)
        if (Physics.Raycast(ray, out RaycastHit enemyHit, range, shootableLayers))
        {
            // Check if hit object has IDamageable (enemy)
            IDamageable damageable = enemyHit.collider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                // Change to red when aiming at enemy
                persistentImage.color = Color.red;
            }
            else
            {
                // Return to original color when not aiming at enemy
                persistentImage.color = originalPersistentColor;
            }
        }
        else
        {
            // Return to original color when no target
            persistentImage.color = originalPersistentColor;
        }
    }
    
    void UpdateAmmoFillTarget()
    {
        if (ammoFillImage != null)
        {
            targetFillAmount = (float)currentAmmo / magSize;
        }
    }
    
    void UpdateAmmoFill()
    {
        if (ammoFillImage != null)
        {
            currentFillAmount = Mathf.Lerp(currentFillAmount, targetFillAmount, Time.deltaTime * fillSmoothSpeed);
            ammoFillImage.fillAmount = currentFillAmount;
        }
    }
    
    private void OnDrawGizmos()
    {
        if (playerCamera != null)
        {
            // Draw shooting ray from camera center
            Gizmos.color = Color.red;
            Vector3 rayOrigin = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f)).origin;
            Vector3 rayDirection = playerCamera.transform.forward * range;
            
            Gizmos.DrawRay(rayOrigin, rayDirection);
            
            // Draw a sphere at the end of the range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(rayOrigin + rayDirection, 0.1f);
            
            // Draw crosshair position
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(rayOrigin, 0.05f);
        }
    }
}
