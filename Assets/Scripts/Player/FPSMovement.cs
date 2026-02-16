using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class FPSMovement : MonoBehaviour
{
    PlayerInput playerInput;
    InputAction moveAction, jumpAction;
    public static FPSMovement Instance;

    Rigidbody rb;
    [SerializeField] Transform cameraHolder;
    [SerializeField] Vector3 currentRotation;

    [SerializeField] float speed = 5f;
    [SerializeField] float jumpForce = 5f;
    [SerializeField] float mouseSensitivity = 200f;

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

        StartCoroutine(PlayFootStep());
    }

    void Update()
    {
        CheckGround();
        MovePlayer();
        HandleMouseLook();
        HandleShake();
        //Look();
    }

    private void OnEnable()
    {
        jumpAction.performed += JumpPlayer;
    }

    private void OnDisable()
    {
        jumpAction.performed -= JumpPlayer;
    }

    void MovePlayer()
    {
        Vector3 val = moveAction.ReadValue<Vector2>();

        Vector3 direction = new Vector3(val.x, 0, val.y);
        Vector3 velocity = direction * speed * Time.deltaTime;
        velocity.y = rb.linearVelocity.y;

        rb.linearVelocity = transform.TransformDirection(velocity);
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
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
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
