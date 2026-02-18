using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Types of UI animations supported by the sequence system.
/// </summary>
public enum UIAnimationType
{
    Scroll,     // Animate RectTransform anchored position
    Transform   // Animate Transform position
}

/// <summary>
/// Types of actions that can be performed in a sequence.
/// Each action represents a different step in the typewriter sequence.
/// </summary>
public enum SequenceActionType
{
    TypeText,     // Type out text with TypewriterEffect
    Wait,         // Wait for specified seconds
    WaitForInput, // Wait for user input (e.g., mouse click)
    UIAnimation,  // Play a UI animation
    SetActive,    // Set active state of a GameObject
}

/// <summary>
/// Represents a single step in a typewriter sequence.
/// Each step can perform different actions like typing text, waiting, or playing animations.
/// </summary>
[System.Serializable]
public class SequenceStep
{
    public SequenceActionType actionType;

    public TextMeshProUGUI targetText;           // Text component to animate
    [TextArea(3, 10)]
    public string textToType = "";                // Text to display
    public float typeSpeed = 20f;                 // Characters per second
    public bool waitForCompletion = true;        // Wait for typing to finish before next step

    public float duration = 1f;                  // Duration to wait in seconds

    public UIAnimationType uiAnimationType;       // Type of animation to play
    public RectTransform targetPanel;             // Panel to scroll (for Scroll type)
    public Transform targetTransform;             // Transform to move (for Transform type)
    public Vector2 startPosition;                 // Start position for Scroll animations
    public Vector2 endPosition;                   // End position for Scroll animations
    public Vector3 startTransformPosition;        // Start position for Transform animations
    public Vector3 endTransformPosition;          // End position for Transform animations
    public float animationDuration = 1f;          // Length of animation
    public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);  // Animation easing

    public GameObject targetObject;                // GameObject to activate/deactivate
    public bool setActiveState = true;            // True to activate, false to deactivate

    public bool enableSkip = false;               // Allow skipping to another step
    [Tooltip("Index of the step to skip to (0-based)")]
    public int skipToIndex = -1;                  // Target step index for skipping
}

/// <summary>
/// Manages a sequence of typewriter effects and UI animations.
/// Controls the timing and execution of multiple steps including text typing,
/// waiting, user input, UI animations, and GameObject activation.
/// </summary>
public class TypewriterSequence : MonoBehaviour
{
    [Header("Sequence Settings")]
    public bool playOnStart = true;               // Auto-start sequence on Start()
    public bool playOnAwake = false;              // Auto-start sequence on Awake()
    public bool loopSequence = false;              // Loop the sequence indefinitely
    [SerializeField] private bool useUnscaledTime = true;  // Use unscaled time for pause-resistant timing
    [SerializeField] private SequenceStep[] sequenceSteps;  // Array of sequence steps to execute

    [Header("Typewriter Audio Settings")]
    [SerializeField] private AudioSource typewriterAudioSource;  // Audio source for typing sounds
    [SerializeField] private AudioClip typewriterSound;          // Sound clip for typing
    [SerializeField] private float typewriterMinPitch = 0.9f;   // Minimum pitch variation
    [SerializeField] private float typewriterMaxPitch = 1.1f;   // Maximum pitch variation

    // Runtime state variables
    private int currentStepIndex = 0;             // Current step being executed
    private TypewriterEffect currentTypewriter;    // Reference to active typewriter effect
    private bool isSequenceRunning = false;       // Flag indicating if sequence is active
    private bool isWaitingForInput = false;       // Flag waiting for user input

    public bool IsRunning => isSequenceRunning;

    /// <summary>
    /// Start sequence automatically if playOnStart is enabled.
    /// </summary>
    private void Start()
    {
        if (playOnStart)
        {
            StartSequence();
        }
    }

    /// <summary>
    /// Initialize component and ensure audio source is available.
    /// </summary>
    private void Awake()
    {
        // Ensure we have a typewriter audio source
        if (typewriterAudioSource == null)
        {
            typewriterAudioSource = gameObject.AddComponent<AudioSource>();
            typewriterAudioSource.playOnAwake = false;
        }

        if(playOnAwake)
        {
            StartSequence();
        }
    }

    /// <summary>
    /// Handle input events for waiting and skipping.
    /// </summary>
    private void Update()
    {
        // Check for user input when waiting for input step
        if (isWaitingForInput && Input.GetMouseButtonDown(0))
        {
            isWaitingForInput = false;
        }

        // Handle skip functionality during sequence
        if (isSequenceRunning && Input.GetMouseButtonDown(0))
        {
            CheckAndPerformSkip();
        }
    }

    /// <summary>
    /// Start executing the sequence from the beginning.
    /// Clears all text components and begins processing steps.
    /// </summary>
    public void StartSequence()
    {
        if (isSequenceRunning) return;  // Prevent multiple simultaneous sequences

        ClearAllTextComponents();  // Reset all text displays
        
        currentStepIndex = 0;
        isSequenceRunning = true;
        ProcessCurrentStep();  // Begin first step
    }

    /// <summary>
    /// Stop the sequence immediately and clean up resources.
    /// </summary>
    public void StopSequence()
    {
        // Stop any active typewriter effect
        if (currentTypewriter != null)
        {
            currentTypewriter.StopAllCoroutines();
            currentTypewriter = null;
        }
        
        // Stop all running coroutines
        StopAllCoroutines();
        isSequenceRunning = false;
        isWaitingForInput = false;
    }

    /// <summary>
    /// Clear all text components used in the sequence.
    /// Called before starting a new sequence to ensure clean state.
    /// </summary>
    private void ClearAllTextComponents()
    {
        if (sequenceSteps == null) return;

        foreach (SequenceStep step in sequenceSteps)
        {
            if (step.targetText != null)
            {
                step.targetText.text = "";  // Clear text display
            }
        }
    }

    /// <summary>
    /// Execute the current step based on its action type.
    /// Handles looping logic and delegates to appropriate handlers.
    /// </summary>
    private void ProcessCurrentStep()
    {
        // Check if we've reached the end of the sequence
        if (currentStepIndex >= sequenceSteps.Length)
        {
            if (loopSequence)
            {
                currentStepIndex = 0;  // Reset to beginning for loop
            }
            else
            {
                isSequenceRunning = false;  // End sequence
                return;
            }
        }

        SequenceStep currentStep = sequenceSteps[currentStepIndex];

        // Execute the appropriate handler based on action type
        switch (currentStep.actionType)
        {
            case SequenceActionType.TypeText:
                StartCoroutine(HandleTypeTextStep(currentStep));
                break;

            case SequenceActionType.Wait:
                StartCoroutine(HandleWaitStep(currentStep.duration));
                break;

            case SequenceActionType.WaitForInput:
                StartCoroutine(HandleWaitForInputStep());
                break;

            case SequenceActionType.UIAnimation:
                StartCoroutine(HandleUIAnimationStep(currentStep));
                break;

            case SequenceActionType.SetActive:
                HandleSetActiveStep(currentStep);
                break;
        }
    }

    /// <summary>
    /// Handle text typing steps using the TypewriterEffect component.
    /// Configures audio settings and manages the typing animation.
    /// </summary>
    private IEnumerator HandleTypeTextStep(SequenceStep step)
    {
        // Check for valid target text
        if (step.targetText == null)
        {
            Debug.LogWarning("No target text assigned for typewriter step!");
            MoveToNextStep();
            yield break;
        }

        // Get or create TypewriterEffect component
        currentTypewriter = step.targetText.GetComponent<TypewriterEffect>();
        if (currentTypewriter == null)
        {
            currentTypewriter = step.targetText.gameObject.AddComponent<TypewriterEffect>();
        }

        // Configure the typewriter effect with our dedicated audio source
        currentTypewriter.charactersPerSecond = step.typeSpeed;
        
        // Use reflection to set private audio settings on TypewriterEffect
        var audioSourceField = typeof(TypewriterEffect).GetField("audioSource", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (audioSourceField != null)
        {
            audioSourceField.SetValue(currentTypewriter, typewriterAudioSource);
        }
        
        var typeSoundField = typeof(TypewriterEffect).GetField("typeSound", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (typeSoundField != null && typewriterSound != null)
        {
            typeSoundField.SetValue(currentTypewriter, typewriterSound);
        }
        
        var minPitchField = typeof(TypewriterEffect).GetField("minPitch", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (minPitchField != null)
        {
            minPitchField.SetValue(currentTypewriter, typewriterMinPitch);
        }
        
        var maxPitchField = typeof(TypewriterEffect).GetField("maxPitch", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (maxPitchField != null)
        {
            maxPitchField.SetValue(currentTypewriter, typewriterMaxPitch);
        }
        
        // Start the typing animation
        currentTypewriter.SetText(step.textToType);
        currentTypewriter.StartTyping();

        // Wait for completion if required
        if (step.waitForCompletion)
        {
            yield return new WaitUntil(() => !currentTypewriter.IsTyping);
        }

        MoveToNextStep();
    }

    /// <summary>
    /// Handle wait steps by pausing for the specified duration.
    /// </summary>
    private IEnumerator HandleWaitStep(float duration)
    {
        // Wait for the specified duration
        yield return useUnscaledTime ? new WaitForSecondsRealtime(duration) : new WaitForSeconds(duration);
        MoveToNextStep();
    }

    /// <summary>
    /// Handle waiting for user input steps.
    /// Pauses the sequence until the user clicks the mouse.
    /// </summary>
    private IEnumerator HandleWaitForInputStep()
    {
        // Set flag to indicate waiting for input
        isWaitingForInput = true;
        
        // Wait until input is received
        while (isWaitingForInput)  
        {
            yield return null;
        }
        MoveToNextStep();
    }

    /// <summary>
    /// Handle UI animation steps (scroll or transform animations).
    /// Supports both blocking and non-blocking animation modes.
    /// </summary>
    private IEnumerator HandleUIAnimationStep(SequenceStep step)
    {
        // Check for valid target panel or transform
        if (step.targetPanel == null && step.targetTransform == null)
        {
            Debug.LogWarning("No target panel or transform assigned for UI animation step!");
            MoveToNextStep();
            yield break;
        }

        // Execute the appropriate animation type
        switch (step.uiAnimationType)
        {
            case UIAnimationType.Scroll:
                if (step.waitForCompletion)
                {
                    yield return StartCoroutine(AnimatePanelScroll(
                        step.targetPanel,
                        step.startPosition,
                        step.endPosition,
                        step.animationDuration,
                        step.animationCurve
                    ));
                }
                else
                {
                    // Start the animation but don't wait for it to complete
                    StartCoroutine(AnimatePanelScroll(
                        step.targetPanel,
                        step.startPosition,
                        step.endPosition,
                        step.animationDuration,
                        step.animationCurve
                    ));
                }
                break;
                
            case UIAnimationType.Transform:
                if (step.waitForCompletion)
                {
                    yield return StartCoroutine(AnimateTransform(
                        step.targetTransform,
                        step.startTransformPosition,
                        step.endTransformPosition,
                        step.animationDuration,
                        step.animationCurve
                    ));
                }
                else
                {
                    // Start the animation but don't wait for it to complete
                    StartCoroutine(AnimateTransform(
                        step.targetTransform,
                        step.startTransformPosition,
                        step.endTransformPosition,
                        step.animationDuration,
                        step.animationCurve
                    ));
                }
                break;
        }

        MoveToNextStep();
    }

    /// <summary>
    /// Animate a RectTransform's anchored position (for UI panels).
    /// Uses Lerp with animation curve for smooth movement.
    /// </summary>
    private IEnumerator AnimatePanelScroll(RectTransform panel, Vector2 startPos, Vector2 endPos, float duration, AnimationCurve curve)
    {
        float elapsed = 0f;
        panel.anchoredPosition = startPos;  // Set initial position

        // Animate over time
        while (elapsed < duration)
        {
            float t = curve.Evaluate(elapsed / duration);  // Evaluate curve
            panel.anchoredPosition = Vector2.Lerp(startPos, endPos, t);  // Interpolate position
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        panel.anchoredPosition = endPos;  // Ensure final position is exact
    }
    
    /// <summary>
    /// Animate a Transform's position (for 3D objects or world space UI).
    /// Uses Lerp with animation curve for smooth movement.
    /// </summary>
    private IEnumerator AnimateTransform(Transform target, Vector3 startPos, Vector3 endPos, float duration, AnimationCurve curve)
    {
        float elapsed = 0f;
        target.position = startPos;  // Set initial position

        // Animate over time
        while (elapsed < duration)
        {
            float t = curve.Evaluate(elapsed / duration);  // Evaluate curve
            target.position = Vector3.Lerp(startPos, endPos, t);  // Interpolate position
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        target.position = endPos;  // Ensure final position is exact
    }

    /// <summary>
    /// Handle SetActive steps by activating or deactivating GameObjects.
    /// </summary>
    private void HandleSetActiveStep(SequenceStep step)
    {
        if (step.targetObject != null)
        {
            step.targetObject.SetActive(step.setActiveState);
        }
        else
        {
            Debug.LogWarning("No target object assigned for SetActive step!");
        }
        
        MoveToNextStep();
    }

    /// <summary>
    /// Advance to the next step in the sequence.
    /// Handles looping logic and sequence termination.
    /// </summary>
    private void MoveToNextStep()
    {
        if (isSequenceRunning)
        {
            currentStepIndex++;
            if (currentStepIndex < sequenceSteps.Length)
            {
                ProcessCurrentStep();  // Continue to next step
            }
            else if (loopSequence)
            {
                currentStepIndex = 0;  // Reset to beginning for loop
                ProcessCurrentStep();
            }
            else
            {
                isSequenceRunning = false;  // End sequence
            }
        }
    }

    /// <summary>
    /// Check if the current step allows skipping and perform the skip.
    /// Called when user clicks during sequence execution.
    /// </summary>
    private void CheckAndPerformSkip()
    {
        if (currentStepIndex < sequenceSteps.Length)
        {
            SequenceStep currentStep = sequenceSteps[currentStepIndex];
            
            // Check if skipping is enabled and target index is valid
            if (currentStep.enableSkip && currentStep.skipToIndex >= 0 && currentStep.skipToIndex < sequenceSteps.Length)
            {
                SkipToStep(currentStep.skipToIndex);
            }
        }
    }

    /// <summary>
    /// Skip to a specific step in the sequence.
    /// Immediately executes all steps before the target and jumps to the specified step.
    /// </summary>
    /// <param name="stepIndex">Index of the step to skip to (0-based)</param>
    public void SkipToStep(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= sequenceSteps.Length)
        {
            return;  // Invalid index
        }

        // Stop any current typewriter effect
        if (currentTypewriter != null)
        {
            currentTypewriter.StopAllCoroutines();
            currentTypewriter = null;
        }

        // Stop any running coroutines
        StopAllCoroutines();
        
        // Reset waiting state
        isWaitingForInput = false;
        
        // Immediately execute all steps before the target index
        for (int i = 0; i < stepIndex; i++)
        {
            ExecuteStepImmediately(sequenceSteps[i]);
        }
        
        // Set the new step index and process
        currentStepIndex = stepIndex;
        ProcessCurrentStep();
    }

    /// <summary>
    /// Execute a step immediately without animations or delays.
    /// Used when skipping to ensure all previous steps are in their final state.
    /// </summary>
    private void ExecuteStepImmediately(SequenceStep step)
    {
        switch (step.actionType)
        {
            case SequenceActionType.TypeText:
                // Show full text immediately
                if (step.targetText != null)
                {
                    step.targetText.text = step.textToType;
                }
                break;

            case SequenceActionType.Wait:
                // Skip wait steps entirely
                break;

            case SequenceActionType.WaitForInput:
                // Skip wait for input steps entirely
                break;

            case SequenceActionType.UIAnimation:
                // Jump to final animation positions
                if (step.targetPanel != null && step.uiAnimationType == UIAnimationType.Scroll)
                {
                    step.targetPanel.anchoredPosition = step.endPosition;
                }
                else if (step.targetTransform != null && step.uiAnimationType == UIAnimationType.Transform)
                {
                    step.targetTransform.position = step.endTransformPosition;
                }
                break;

            case SequenceActionType.SetActive:
                // Apply active state immediately
                if (step.targetObject != null)
                {
                    step.targetObject.SetActive(step.setActiveState);
                }
                break;
        }
    }
}