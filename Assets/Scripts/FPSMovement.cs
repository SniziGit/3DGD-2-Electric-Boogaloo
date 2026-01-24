using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class FPSMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runSpeed = 10f;
    public float jumpForce = 5f;
    
    [Header("Mouse Look")]
    public float mouseSensitivity = 100f;
    public float maxLookAngle = 80f;
    
    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;
    
    private Rigidbody rb;
    private Camera playerCamera;
    
    private float xRotation = 0f;
    private bool isGrounded;
    
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool jumpInput;
    private bool runInput;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerCamera = GetComponentInChildren<Camera>();
        
        if (playerCamera == null)
        {
            Debug.LogError("No camera found as child of this GameObject!");
            return;
        }
        
        // Configure Rigidbody
        rb.freezeRotation = true;
        
        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    void OnEnable()
    {
        // Enable input actions
        EnableInputActions();
    }
    
    void OnDisable()
    {
        // Disable input actions
        DisableInputActions();
    }
    
    void EnableInputActions()
    {
        // These methods will be called by the Input System
        // Make sure to set up the Input Actions asset in Unity
    }
    
    void DisableInputActions()
    {
        // Clean up input actions
    }
    
    // Input System callback methods
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }
    
    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }
    
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
            jumpInput = true;
        else if (context.canceled)
            jumpInput = false;
    }
    
    public void OnRun(InputAction.CallbackContext context)
    {
        if (context.performed)
            runInput = true;
        else if (context.canceled)
            runInput = false;
    }
    
    void Update()
    {
        if (playerCamera == null) return;
        
        GroundCheck();
        HandleMouseLook();
        HandleMovement();
        HandleJump();
    }
    
    void FixedUpdate()
    {
        // Movement is handled in FixedUpdate for Rigidbody physics
    }
    
    void GroundCheck()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
    }
    
    void HandleMouseLook()
    {
        float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;
        
        // Rotate player around y-axis
        transform.Rotate(Vector3.up * mouseX);
        
        // Rotate camera around x-axis
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);
        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }
    
    void HandleMovement()
    {
        float horizontal = moveInput.x;
        float vertical = moveInput.y;
        
        Vector3 direction = transform.right * horizontal + transform.forward * vertical;
        
        // Check if running
        float currentSpeed = runInput ? runSpeed : walkSpeed;
        
        // Apply movement force in FixedUpdate
        Vector3 moveForce = direction * currentSpeed;
        rb.linearVelocity = new Vector3(moveForce.x, rb.linearVelocity.y, moveForce.z);
    }
    
    void HandleJump()
    {
        if (jumpInput && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            jumpInput = false; // Prevent continuous jumping
        }
    }
    
    // Unlock cursor when ESC is pressed
    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
