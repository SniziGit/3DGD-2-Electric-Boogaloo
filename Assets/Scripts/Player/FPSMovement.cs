using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class FPSMovement : MonoBehaviour
{
    PlayerInput playerInput;
    InputAction moveAction, jumpAction, sprintAction, crouchAction;
    // Remove singleton pattern to support multiple players
    // public static FPSMovement Instance;

    [Header("CameraSettings")]
    Rigidbody rb;
    [SerializeField] Transform cameraHolder;
    [SerializeField] Camera playerCamera;
    [SerializeField] Vector3 currentRotation;
    
    [Header("Animation")]
    [SerializeField] Animator playerAnimator;
    
    [Header("Effects")]
    [SerializeField] GameObject runEffect;
    [SerializeField] Transform runEffectSpawnPoint;
    [SerializeField] float effectLifetime = 0.05f;
    [SerializeField] float effectSpawnRate = 0.1f;
    private float effectSpawnTimer;
    
    [Header("Stamina")]
    [SerializeField] UnityEngine.UI.Image staminaFillImage;
    [SerializeField] float maxStamina = 100f;
    [SerializeField] float staminaDrainRate = 20f;
    [SerializeField] float staminaRegenRate = 10f;
    [SerializeField] float fillSmoothSpeed = 5f;
    private float currentStamina;
    private float currentStaminaFill;
    private float targetStaminaFill;
    
    [Header("Pickup System")]
    [SerializeField] float pickupRange = 3f;
    [SerializeField] LayerMask pickupLayerMask; // Keep for compatibility but not used
    private InputAction pickupAction;
    
    [Header("Revive System")]
    [SerializeField] private ReviveInteraction reviveInteraction;
    private PlayerHealth playerHealth;

    [Header("Movement")]
    [SerializeField] float normalSpeed = 800f;
    [SerializeField] float sprintSpeed = 2000f;
    [SerializeField] float crouchSpeed = 550f;
    private float speed;
    [SerializeField] float normalFOV = 100f;
    [SerializeField] float sprintFOV = 120f;
    [SerializeField] float crouchFOV = 90f;
    private float targetFOV;
    [SerializeField] float jumpForce = 5f;
    [SerializeField] float mouseSensitivity = 200f;
    private bool isPressed;
    private bool isCrouching;
    private bool isShooting;
    private bool isRunning;


    [Header("GroundCheck")]
    [SerializeField] Transform groundCheck;
    [SerializeField] float groundDistance = 0.4f;
    [SerializeField] LayerMask groundMask;
    private bool isGrounded;

    //Camera
    private float xRotation = 0f;
    private Vector2 lookInput;

    private float shakeDuration = 0f;
    private float shakeMagnitude = 0.1f;
    private float shakeFadeSpeed = 0.5f;
    private Vector3 initialCamPos;

    public AudioClip footStepSFX;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        
        // Try to force keyboard and mouse control scheme (remove if causing issues)
        if (playerInput != null)
        {
            // Don't force control scheme - let Unity handle it automatically
            // playerInput.SwitchCurrentControlScheme("Keyboard&Mouse");
        }
        
        moveAction = playerInput.actions.FindAction("Move");
        jumpAction = playerInput.actions.FindAction("Jump");
        sprintAction = playerInput.actions.FindAction("Sprint");
        crouchAction = playerInput.actions.FindAction("Crouch");
        pickupAction = playerInput.actions.FindAction("Interact"); // Add pickup action
        //lookAction = playerInput.actions.FindAction("Look");
        rb = GetComponent<Rigidbody>();
        playerAnimator = GetComponent<Animator>();
        playerHealth = GetComponent<PlayerHealth>(); // Get PlayerHealth from same GameObject
        
        //Take dmg visual - remove singleton to support multiple players
        // Instance = this;
        
        // Find camera if not assigned
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    void Start()
    {
        currentRotation = transform.eulerAngles;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        initialCamPos = cameraHolder.localPosition;
        speed = normalSpeed;
        targetFOV = normalFOV;
        
        // Initialize stamina
        currentStamina = maxStamina;
        currentStaminaFill = 1f;
        targetStaminaFill = 1f;
        
        if (playerCamera == null)
            playerCamera = Camera.main;
        
        if (staminaFillImage != null)
            staminaFillImage.fillAmount = currentStaminaFill;

        StartCoroutine(PlayFootStep());
    }

    void Update()
    {
        // Check if player is downed - if so, disable movement
        if (playerHealth != null && playerHealth.IsDowned())
        {
            // Disable all movement when downed
            rb.linearVelocity = Vector3.zero;
            return;
        }
        
        CheckGround();
        MovePlayer();
        HandleMouseLook();
        HandleShake();
        HandleFOV();
        UpdateAnimations();
        HandleRunEffect();
        HandleStamina();
        CheckHealthPickups();
        CheckReviveTargets();
        
        // Cancel sprint if out of stamina
        if (isRunning && currentStamina <= 0)
        {
            OnSprintCanceled(new InputAction.CallbackContext());
        }
        //Look();
    }

    private void OnEnable()
    {
        jumpAction.performed += JumpPlayer;
        sprintAction.started += OnSprintStarted;
        sprintAction.canceled += OnSprintCanceled;
        crouchAction.started += OnCrouchStarted;
        crouchAction.canceled += OnCrouchCanceled;
        if (pickupAction != null)
            pickupAction.performed += OnPickupPerformed;
    }

    private void OnDisable()
    {
        if (jumpAction != null)
            jumpAction.performed -= JumpPlayer;
        if (sprintAction != null)
        {
            sprintAction.started -= OnSprintStarted;
            sprintAction.canceled -= OnSprintCanceled;
        }
        if (crouchAction != null)
        {
            crouchAction.started -= OnCrouchStarted;
            crouchAction.canceled -= OnCrouchCanceled;
        }
        if (pickupAction != null)
            pickupAction.performed -= OnPickupPerformed;
    }

    void MovePlayer()
    {
        Vector3 val = moveAction.ReadValue<Vector2>();

        Vector3 direction = new Vector3(val.x, 0, val.y);
        Vector3 velocity = direction * speed * Time.deltaTime;
        velocity.y = rb.linearVelocity.y;

        rb.linearVelocity = transform.TransformDirection(velocity);
    }

    void OnSprintStarted(InputAction.CallbackContext context)
    {
        if (isCrouching)
        {
            // Cancel crouch if sprint is pressed
            OnCrouchCanceled(context);
        }
        
        // Only allow sprinting if we have stamina
        if (currentStamina > 0)
        {
            isPressed = true;
            isRunning = true;
            speed = sprintSpeed;
            targetFOV = sprintFOV;
        }
    }

    void OnSprintCanceled(InputAction.CallbackContext context)
    {
        isPressed = false;
        isRunning = false;
        speed = normalSpeed;
        targetFOV = normalFOV;
    }

    void OnCrouchStarted(InputAction.CallbackContext context)
    {
        if (isRunning)
        {
            // Cancel sprint if crouch is pressed
            OnSprintCanceled(context);
        }
        isPressed = true;
        isCrouching = true;
        speed = crouchSpeed;
        targetFOV = crouchFOV;
        playerAnimator.SetBool("PlayerCrouch", true);
        playerAnimator.SetBool("PlayerIdle", false);
    }

    void OnCrouchCanceled(InputAction.CallbackContext context)
    {
        isPressed = false;
        isCrouching = false;
        speed = normalSpeed;
        targetFOV = normalFOV;
        playerAnimator.SetBool("PlayerCrouch", false);
    }

    void JumpPlayer(InputAction.CallbackContext context)
    {
        if (isGrounded)
        {
            rb.AddForce(new Vector3(0, jumpForce, 0), ForceMode.Impulse);
        }
    }

    void CheckGround()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
    }

    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }

    void HandleMouseLook()
    {
        float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 60f);

        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleFOV()
    {
        if (playerCamera != null)
        {
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, Time.deltaTime * 10f);
        }
    }

    void HandleShake()
    {
        if (shakeDuration > 0)
        {
            cameraHolder.localPosition = initialCamPos + Random.insideUnitSphere * shakeMagnitude;
            shakeDuration -= Time.deltaTime * shakeFadeSpeed;
        }
        else
        {
            cameraHolder.localPosition = initialCamPos;
        }
    }

    public void AddShake(float duration, float magnitude)
    {
        shakeDuration = duration;
        shakeMagnitude = magnitude;
    }

    IEnumerator PlayFootStep()
    {
        while (true)
        {
            if (rb.linearVelocity.magnitude > 0.1f && isGrounded)
            {
               AudioManager.Instance.PlaySFX(footStepSFX);
            }
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    void UpdateAnimations()
    {
        // Handle idle animation when not moving and not crouching
        if (rb.linearVelocity.magnitude < 0.1f && !isCrouching && !isShooting)
        {
            playerAnimator.SetBool("PlayerIdle", true);
        }
        else if (rb.linearVelocity.magnitude > 0.1f)
        {
            playerAnimator.SetBool("PlayerIdle", false);
        }
        
        // Handle shooting animation (this would be called from the gun script)
        if (isShooting && !isCrouching)
        {
            playerAnimator.SetBool("PlayerShooting", true);
            playerAnimator.SetBool("PlayerIdle", false);
        }
        else
        {
            playerAnimator.SetBool("PlayerShooting", false);
        }
    }
    
    public void SetShooting(bool shooting)
    {
        isShooting = shooting;
    }
    
    void HandleRunEffect()
    {
        if (isRunning && isGrounded && rb.linearVelocity.magnitude > 0.1f && moveAction.ReadValue<Vector2>().magnitude > 0.1f && currentStamina > 0)
        {
            effectSpawnTimer -= Time.deltaTime;
            if (effectSpawnTimer <= 0f)
            {
                InstantiateRunEffect();
                effectSpawnTimer = effectSpawnRate;
            }
        }
    }
    
    void InstantiateRunEffect()
    {
        if (runEffect != null && runEffectSpawnPoint != null)
        {
            GameObject effect = Instantiate(runEffect, runEffectSpawnPoint.position, runEffectSpawnPoint.rotation);
            Destroy(effect, effectLifetime);
        }
    }
    
    void HandleStamina()
    {
        if (isRunning && isGrounded && rb.linearVelocity.magnitude > 0.1f && moveAction.ReadValue<Vector2>().magnitude > 0.1f)
        {
            // Drain stamina while running
            currentStamina = Mathf.Max(0, currentStamina - staminaDrainRate * Time.deltaTime);
        }
        else if (!isCrouching)
        {
            // Regenerate stamina when not running or crouching
            currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenRate * Time.deltaTime);
        }
        
        // Update stamina fill
        UpdateStaminaFill();
    }
    
    void UpdateStaminaFill()
    {
        if (staminaFillImage != null)
        {
            targetStaminaFill = currentStamina / maxStamina;
            currentStaminaFill = Mathf.Lerp(currentStaminaFill, targetStaminaFill, Time.deltaTime * fillSmoothSpeed);
            staminaFillImage.fillAmount = currentStaminaFill;
        }
    }
    
    void OnPickupPerformed(InputAction.CallbackContext context)
    {
        // Check for revive first
        if (TryRevivePlayer())
        {
            return;
        }
        
        TryPickupHealth();
    }
    
    void CheckHealthPickups()
    {
        if (pickupAction == null || playerCamera == null) return;
        
        // Raycast from camera center to check for pickups
        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        
        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupLayerMask))
        {
            // Check for health pickups
            HealthPickup healthPickup = hit.collider.GetComponent<HealthPickup>();
            if (healthPickup != null)
            {
                // Show pickup prompt or auto-pickup
                // For now, we'll auto-pickup when F is pressed
            }
            
            // Check for stamina pickups
            StaminaPickup staminaPickup = hit.collider.GetComponent<StaminaPickup>();
            if (staminaPickup != null)
            {
                // Show pickup prompt or auto-pickup
                // For now, we'll auto-pickup when F is pressed
            }
        }
    }
    
    void TryPickupHealth()
    {
        if (pickupAction == null || playerCamera == null) return;
        
        // Raycast from camera center to pickup items
        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        
        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupLayerMask))
        {
            // Check for health pickups
            HealthPickup healthPickup = hit.collider.GetComponent<HealthPickup>();
            if (healthPickup != null)
            {
                // Check if player is at full health
                if (playerHealth != null && playerHealth.IsFullHealth())
                {
                    Debug.Log("Already at full health!");
                    return;
                }
                
                // Pickup the health item
                PickupHealthItem(healthPickup);
                return;
            }
            
            // Check for stamina pickups
            StaminaPickup staminaPickup = hit.collider.GetComponent<StaminaPickup>();
            if (staminaPickup != null)
            {
                // Check if player is at full stamina
                if (currentStamina >= maxStamina)
                {
                    Debug.Log("Already at full stamina!");
                    return;
                }
                
                // Pickup the stamina item
                PickupStaminaItem(staminaPickup);
                return;
            }
        }
    }
    
    void PickupHealthItem(HealthPickup healthPickup)
    {
        // Add health to player using the pickup's own amount
        if (playerHealth != null)
        {
            playerHealth.Heal(healthPickup.GetHealthAmount());
        }
        
        // Destroy the pickup object
        Destroy(healthPickup.gameObject);
        
        Debug.Log($"Picked up health: +{healthPickup.GetHealthAmount()} HP");
    }
    
    void PickupStaminaItem(StaminaPickup staminaPickup)
    {
        // Add stamina to player
        if (staminaPickup.ShouldMaxOutStamina())
        {
            // Max out stamina
            currentStamina = maxStamina;
            Debug.Log("Picked up stamina: Maxed out!");
        }
        else
        {
            // Add specific amount
            currentStamina = Mathf.Min(maxStamina, currentStamina + staminaPickup.GetStaminaAmount());
            Debug.Log($"Picked up stamina: +{staminaPickup.GetStaminaAmount()}");
        }
        
        // Update stamina fill immediately
        targetStaminaFill = currentStamina / maxStamina;
        currentStaminaFill = targetStaminaFill;
        
        // Destroy the pickup object
        Destroy(staminaPickup.gameObject);
    }
    
    void CheckReviveTargets()
    {
        if (reviveInteraction == null || playerHealth == null || playerHealth.IsDowned())
            return;
        
        // Check if there are downed players in range
        if (reviveInteraction.CanRevive())
        {
            // Show revive prompt (you could add UI here)
            // For now, we'll just log it
            // Debug.Log("Press F to revive nearby player");
        }
    }
    
    bool TryRevivePlayer()
    {
        if (reviveInteraction == null || playerHealth == null || playerHealth.IsDowned())
            return false;
        
        if (reviveInteraction.CanRevive())
        {
            reviveInteraction.StartRevive();
            return true;
        }
        
        return false;
    }

    //void Look()
    //{
    //    if (!lookAction.enabled)
    //    {
    //        Debug.Log("Look action is not enabled!");
    //        return;
    //    }

    //    Vector2 mouseDelta = lookAction.ReadValue<Vector2>();
    //    Debug.Log("Raw mouse delta: " + mouseDelta);

    //    currentRotation.x += mouseDelta.x * mouseSensitivity;
    //    currentRotation.y -= mouseDelta.y * mouseSensitivity;

    //    cameraHolder.rotation = Quaternion.Euler(new Vector3(currentRotation.y, currentRotation.x, 0));
    //    rb.rotation = Quaternion.Euler(new Vector3(0, currentRotation.x, 0));
    //}


}
