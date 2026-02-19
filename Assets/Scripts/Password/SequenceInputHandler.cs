using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System;

public class SequenceInputHandler : MonoBehaviour
{
    private PlayerInput playerInput;
    private InputAction passwordUpAction, passwordDownAction, passwordLeftAction, passwordRightAction;
    private DirectionalSequenceUI ui;

    private List<PasswordNode.Direction> targetSequence;
    private int currentIndex = 0;
    private bool isListening = false;
    private System.Action<bool> onCompleteCallback;
    
    [Header("Input Settings")]
    [SerializeField] private float inputTimeout = 10f;
    private float inputTimer = 0f;
    
    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
            playerInput = GetComponentInParent<PlayerInput>();

        if (playerInput != null)
        {
            passwordUpAction = playerInput.actions.FindAction("PasswordUp");
            passwordDownAction = playerInput.actions.FindAction("PasswordDown");
            passwordLeftAction = playerInput.actions.FindAction("PasswordLeft");
            passwordRightAction = playerInput.actions.FindAction("PasswordRight");
        }
    }

    public int GetPlayerIndex()
    {
        return playerInput != null ? playerInput.playerIndex : -1;
    }

    public void SetUI(DirectionalSequenceUI sequenceUi)
    {
        ui = sequenceUi;
    }
    
    private void Update()
    {
        if (isListening)
        {
            inputTimer += Time.deltaTime;
            
            // Check for timeout
            if (inputTimer >= inputTimeout)
            {
                Debug.Log("Input timeout!");
                StopListening(false);
            }
        }
    }
    
    public void StartListening(List<PasswordNode.Direction> sequence, System.Action<bool> onComplete)
    {
        if (playerInput == null)
        {
            Debug.LogError("[SequenceInputHandler] PlayerInput not found on this player.");
            onComplete?.Invoke(false);
            return;
        }

        targetSequence = sequence;
        currentIndex = 0;
        isListening = true;
        inputTimer = 0f;
        onCompleteCallback = onComplete;

        // Enable input listening
        EnableInputListening();
    }

    public void StopListeningExternal(bool success)
    {
        if (!isListening)
            return;

        StopListening(success);
    }
    
    private void EnableInputListening()
    {
        if (passwordUpAction != null)
            passwordUpAction.performed += OnUpInput;
        if (passwordDownAction != null)
            passwordDownAction.performed += OnDownInput;
        if (passwordLeftAction != null)
            passwordLeftAction.performed += OnLeftInput;
        if (passwordRightAction != null)
            passwordRightAction.performed += OnRightInput;
    }
    
    private void DisableInputListening()
    {
        if (passwordUpAction != null)
            passwordUpAction.performed -= OnUpInput;
        if (passwordDownAction != null)
            passwordDownAction.performed -= OnDownInput;
        if (passwordLeftAction != null)
            passwordLeftAction.performed -= OnLeftInput;
        if (passwordRightAction != null)
            passwordRightAction.performed -= OnRightInput;
    }
    
    private void OnUpInput(InputAction.CallbackContext context)
    {
        if (isListening) ProcessInput(PasswordNode.Direction.Up);
    }
    
    private void OnDownInput(InputAction.CallbackContext context)
    {
        if (isListening) ProcessInput(PasswordNode.Direction.Down);
    }
    
    private void OnLeftInput(InputAction.CallbackContext context)
    {
        if (isListening) ProcessInput(PasswordNode.Direction.Left);
    }
    
    private void OnRightInput(InputAction.CallbackContext context)
    {
        if (isListening) ProcessInput(PasswordNode.Direction.Right);
    }
    
    private void ProcessInput(PasswordNode.Direction inputDirection)
    {
        if (currentIndex >= targetSequence.Count)
        {
            Debug.LogError("Current index exceeds sequence length!");
            return;
        }
        
        bool isCorrect = (inputDirection == targetSequence[currentIndex]);

        if (ui != null)
            ui.ShowInputFeedback(currentIndex, isCorrect);
        
        if (isCorrect)
        {
            currentIndex++;
            Debug.Log($"Correct input! Progress: {currentIndex}/{targetSequence.Count}");
            
            // Check if sequence is complete
            if (currentIndex >= targetSequence.Count)
            {
                Debug.Log("Sequence completed successfully!");
                StopListening(true);
            }
        }
        else
        {
            Debug.Log($"Wrong input! Expected {targetSequence[currentIndex]}, got {inputDirection}");
            StopListening(false);
        }
    }
    
    private void StopListening(bool success)
    {
        isListening = false;
        DisableInputListening();

        if (ui != null)
            ui.ShowResult(success);
        
        // Call completion callback
        onCompleteCallback?.Invoke(success);
    }
    
    // Alternative method using individual key actions
    private void SetupIndividualActions()
    {
        // This would require adding separate actions to PlayerControls.inputactions
        // For now, using the Move action approach above
    }
    
    private void OnDestroy()
    {
        DisableInputListening();
    }
    
    // For manual input testing (can be called from UI buttons)
    public void ManualInput(string direction)
    {
        if (!isListening) return;
        
        PasswordNode.Direction inputDirection;
        switch (direction.ToLower())
        {
            case "up":
                inputDirection = PasswordNode.Direction.Up;
                break;
            case "down":
                inputDirection = PasswordNode.Direction.Down;
                break;
            case "left":
                inputDirection = PasswordNode.Direction.Left;
                break;
            case "right":
                inputDirection = PasswordNode.Direction.Right;
                break;
            default:
                return;
        }
        
        ProcessInput(inputDirection);
    }
}
