using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSetup : MonoBehaviour
{
    public PlayerInput player1;
    public PlayerInput player2;

    void Start()
    {
        player1.SwitchCurrentControlScheme(
            "Keyboard Mouse",
            Keyboard.current,
            Mouse.current
        );

        player2.SwitchCurrentControlScheme(
            "Gamepad",
            Gamepad.current
        );
    }
}
