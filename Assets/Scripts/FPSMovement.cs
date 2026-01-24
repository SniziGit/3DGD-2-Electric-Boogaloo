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
    
    private PlayerInput playerInput;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerCamera = GetComponentInChildren<Camera>();
        playerInput = GetComponent<PlayerInput>();
        
        if (playerCamera == null)
        {
            Debug.LogError("No camera found as child of this GameObject!");
            return;
        }
        
        if (playerInput == null)
        {
            Debug.LogError("No PlayerInput component found! Please add PlayerInput component and assign the InputSystem_Actions asset.");
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
        // Input actions are handled by PlayerInput component
        // Make sure PlayerInput component is configured with the InputSystem_Actions asset
    }
    
    void DisableInputActions()
    {
        // Input actions are handled by PlayerInput component
    }
    
    // Input System callback methods for PlayerInput
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }
    
    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }
    
    public void OnJump(InputValue value)
    {
        jumpInput = value.isPressed;
    }
    
    public void OnSprint(InputValue value)
    {
        runInput = value.isPressed;
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
