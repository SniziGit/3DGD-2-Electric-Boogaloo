using UnityEngine;
using UnityEngine.InputSystem;

public class FPSMovement : MonoBehaviour
{
    PlayerInput playerInput;
    InputAction moveAction, jumpAction, lookAction;

    Rigidbody rb;

    [SerializeField] float speed = 5f;
    void OnMove(InputValue value)
    {
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions.FindAction("Move");
        jumpAction = playerInput.actions.FindAction("Jump");
        lookAction = playerInput.actions.FindAction("Look");
    }

    void Update()
    {
        MovePlayer();
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

        rb.linearVelocity = direction * speed * Time.deltaTime;
    }
    void JumpPlayer(InputAction.CallbackContext context)
    {
        //if (jumpAction.triggered)
            //Vector2 jumpHeight = jumpAction.ReadValue<Vector2>();
    }
}
