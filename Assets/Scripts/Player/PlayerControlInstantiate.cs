using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControlInstantiate : MonoBehaviour
{
    public GameObject playerPrefab1;
    public GameObject playerPrefab2;

    void Start()
    {
        var p1 = Instantiate(playerPrefab1);
        var p2 = Instantiate(playerPrefab2);

        var input1 = p1.GetComponent<PlayerInput>();
        var input2 = p2.GetComponent<PlayerInput>();

        // Player 1 Keyboard
        input1.SwitchCurrentControlScheme("Keyboard Mouse", Keyboard.current);

        // Player 2 Gamepad
        input2.SwitchCurrentControlScheme("Gamepad", Gamepad.current);
    }
}