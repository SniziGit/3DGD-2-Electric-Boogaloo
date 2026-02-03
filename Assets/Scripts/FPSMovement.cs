using UnityEngine;
using UnityEngine.InputSystem;

public class FPSMovement : MonoBehaviour
{
    public float speed = 5f;
    private Vector2 moveInput;

    void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    void Update()
    {
        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y);
        transform.Translate(move * speed * Time.deltaTime, Space.World);
    }
}
