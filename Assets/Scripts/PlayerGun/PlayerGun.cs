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
    public float recoilAmount = 5f;
    public float recoilDuration = 0.2f;
    public float recoilRecoverySpeed = 5f;
    
    [Header("Effects")]
    public Camera playerCamera;
    public LayerMask shootableLayers;
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
    private bool isHoldingShoot = false;

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
        
        if (recoilCrosshair != null)
        {
            originalCrosshairPosition = recoilCrosshair.transform.localPosition;
            recoilCrosshair.SetActive(false); // Start hidden
        }
    }

    void Update()
    {
        HandleShooting();
        UpdateCrosshairRecoil();
        UpdateAmmoFill();
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
        // Raycast from camera center (crosshair position)
        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        
        if (Physics.Raycast(ray, out RaycastHit hit, range, shootableLayers))
        {
            // Apply damage to hit target
            IDamageable damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }
            
            Debug.Log($"Hit {hit.collider.name} at distance {hit.distance}");
        }
        else
        {
            Debug.Log("Shot missed - no target in range");
        }
        
        // Apply crosshair recoil
        ApplyRecoil();
    }
    
    void ApplyRecoil()
    {
        if (recoilCrosshair != null)
        {
            // Show the recoil crosshair
            recoilCrosshair.SetActive(true);
            
            // Add random recoil offset
            Vector3 randomRecoil = new Vector3(
                Random.Range(-recoilAmount, recoilAmount),
                Random.Range(-recoilAmount, recoilAmount),
                0f
            );
            currentRecoilOffset += randomRecoil;
            
            // Reset recoil timer
            recoilTimer = recoilDuration;
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
        if (recoilCrosshair != null)
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
                // Hide the recoil crosshair when timer is done
                recoilCrosshair.SetActive(false);
                currentRecoilOffset = Vector3.zero;
            }
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
