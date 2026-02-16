using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class FPSMovement : MonoBehaviour
{
    PlayerInput playerInput;
    InputAction moveAction, jumpAction, sprintAction, crouchAction;
    public static FPSMovement Instance;

    [Header("CameraSettings")]
    Rigidbody rb;
    [SerializeField] Transform cameraHolder;
    [SerializeField] Camera playerCamera;
    [SerializeField] Vector3 currentRotation;

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
        moveAction = playerInput.actions.FindAction("Move");
        jumpAction = playerInput.actions.FindAction("Jump");
        sprintAction = playerInput.actions.FindAction("Sprint");
        crouchAction = playerInput.actions.FindAction("Crouch");
        //lookAction = playerInput.actions.FindAction("Look");
        rb = GetComponent<Rigidbody>();

        //Take dmg visual
        Instance = this;
    }

    void Start()
    {
        currentRotation = transform.eulerAngles;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        initialCamPos = cameraHolder.localPosition;
        speed = normalSpeed;
        targetFOV = normalFOV;
        
        if (playerCamera == null)
            playerCamera = Camera.main;

        StartCoroutine(PlayFootStep());
    }

    void Update()
    {
        CheckGround();
        MovePlayer();
        HandleMouseLook();
        HandleShake();
        HandleFOV();
        //Look();
    }

    private void OnEnable()
    {
        jumpAction.performed += JumpPlayer;
        sprintAction.started += OnSprintStarted;
        sprintAction.canceled += OnSprintCanceled;
        crouchAction.started += OnCrouchStarted;
        crouchAction.canceled += OnCrouchCanceled;
    }

    private void OnDisable()
    {
        jumpAction.performed -= JumpPlayer;
        sprintAction.started -= OnSprintStarted;
        sprintAction.canceled -= OnSprintCanceled;
        crouchAction.started -= OnCrouchStarted;
        crouchAction.canceled -= OnCrouchCanceled;
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
        isPressed = true;
        speed = sprintSpeed;
        targetFOV = sprintFOV;
    }

    void OnSprintCanceled(InputAction.CallbackContext context)
    {
        isPressed = false;
        speed = normalSpeed;
        targetFOV = normalFOV;
    }

    void OnCrouchStarted(InputAction.CallbackContext context)
    {
        isPressed = true;
        speed = crouchSpeed;
        targetFOV = crouchFOV;
    }

    void OnCrouchCanceled(InputAction.CallbackContext context)
    {
        isPressed = false;
        speed = normalSpeed;
        targetFOV = normalFOV;
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
