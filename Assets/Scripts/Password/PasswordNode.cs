using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;

public class PasswordNode : MonoBehaviour, IInteractable
{
    [Header("Password Settings")]
    [SerializeField] private int sequenceLength = 4;
    [SerializeField] private float sequenceDisplayTime = 5f;
    [SerializeField] private int maxAttempts = 3;
    
    [Header("Door Integration")]
    [SerializeField] private DoorAnimTrigger doorToUnlock;
    [SerializeField] private string unlockDoorFunction = "UnlockDoor";
    
    [Header("UI")]
    private DirectionalSequenceUI sequenceUI;
    
    private List<Direction> currentSequence;
    private int currentAttempt = 0;
    private bool isInteracting = false;
    
    public enum Direction
    {
        Up, Down, Left, Right
    }
    
    private void Start()
    {
        if (sequenceUI == null)
        {
            sequenceUI = FindObjectOfType<DirectionalSequenceUI>();
        }
    }
    
    public void Interact(GameObject player)
    {
        if (isInteracting) return;
        
        StartPasswordSequence();
    }
    
    private void StartPasswordSequence()
    {
        if (currentAttempt >= maxAttempts)
        {
            Debug.Log("Max attempts reached!");
            return;
        }
        
        isInteracting = true;
        currentSequence = GenerateRandomSequence();
        
        // Show the sequence to the player
        if (sequenceUI != null)
        {
            sequenceUI.ShowSequence(currentSequence, sequenceDisplayTime, OnSequenceDisplayComplete);
        }
        else
        {
            Debug.LogError("DirectionalSequenceUI not found!");
            isInteracting = false;
        }
    }
    
    private List<Direction> GenerateRandomSequence()
    {
        List<Direction> sequence = new List<Direction>();
        System.Random random = new System.Random();
        
        for (int i = 0; i < sequenceLength; i++)
        {
            sequence.Add((Direction)random.Next(0, 4));
        }
        
        return sequence;
    }
    
    private void OnSequenceDisplayComplete()
    {
        // Start listening for player input
        SequenceInputHandler inputHandler = FindObjectOfType<SequenceInputHandler>();
        if (inputHandler != null)
        {
            inputHandler.StartListening(currentSequence, OnInputComplete);
        }
        else
        {
            Debug.LogError("SequenceInputHandler not found!");
            isInteracting = false;
        }
    }
    
    private void OnInputComplete(bool success)
    {
        if (success)
        {
            OnPasswordCorrect();
        }
        else
        {
            OnPasswordWrong();
        }
    }
    
    private void OnPasswordCorrect()
    {
        Debug.Log("Password correct! Unlocking door...");
        
        // Unlock the door
        if (doorToUnlock != null)
        {
            doorToUnlock.Invoke(unlockDoorFunction, 0);
        }
        
        ResetPasswordNode();
    }
    
    private void OnPasswordWrong()
    {
        Debug.Log($"Password wrong! Attempt {currentAttempt + 1}/{maxAttempts}");
        
        currentAttempt++;
        
        if (currentAttempt >= maxAttempts)
        {
            Debug.Log("Max attempts reached! Password node locked.");
            ResetPasswordNode();
        }
        else
        {
            // Allow retry
            isInteracting = false;
        }
    }
    
    private void ResetPasswordNode()
    {
        isInteracting = false;
        currentAttempt = 0;
        currentSequence = null;
    }
    
    // For testing purposes
    public void ForceUnlock()
    {
        OnPasswordCorrect();
    }
    
    public bool CanInteract(GameObject player)
    {
        return !isInteracting && currentAttempt < maxAttempts;
    }
    
    public string GetInteractionName()
    {
        if (currentAttempt >= maxAttempts)
            return "Password Node Locked";
        return "Enter Password Sequence";
    }
    
    public float GetInteractionRange()
    {
        return 3f; // Default interaction range
    }
}
