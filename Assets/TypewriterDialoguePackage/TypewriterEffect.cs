using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Creates a typewriter effect for TextMeshProUGUI components,
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class TypewriterEffect : MonoBehaviour
{
    [Header("Typing Settings")]
    public float charactersPerSecond = 20f;  // Speed of typing effect
    [SerializeField] private float startDelay = 0.5f;        // Delay before typing starts
    [SerializeField] private float punctuationDelay = 0.2f;  // Extra delay for punctuation marks
    [SerializeField] private float cursorBlinkRate = 0.5f;   // Speed of cursor blinking
    [SerializeField] private bool useUnscaledTime = true;    // Use unscaled time for typing that shouldnt be affected by Pause

    [Header("Behaviour Toggles")]
    [Tooltip("Enable or disable the blinking cursor effect during typing.")]
    public bool enableCursor = true;

    [Tooltip("Enable or disable extra delays on punctuation for more natural pacing.")]
    public bool enablePunctuationDelays = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;  // Audio source for typing sounds
    [SerializeField] private AudioClip typeSound;      // Sound played for each character
    [SerializeField] private float minPitch = 0.9f;    // Minimum pitch variation for typing sound
    [SerializeField] private float maxPitch = 1.1f;    // Maximum pitch variation for typing sound

    // Component references and bool variables
    private TextMeshProUGUI textComponent;  // The TextMeshPro component being animated
    private string fullText;                 // Complete text to be displayed
    private Coroutine typingCoroutine;       // Coroutine handling the typing animation
    private Coroutine cursorCoroutine;       // Coroutine handling cursor blinking
    private bool cursorVisible = true;       // Current visibility state of cursor
    private bool isTyping = false;           // Flag indicating if typing is in progress
    private int typedVisibleCharCount = 0;   // Counter for non-whitespace characters typed

    // Public properties and events
    public bool IsTyping => isTyping;         // Read-only property to check typing status
    public event System.Action OnTypingComplete;  // Event called when typing completes

    // Cursor display constants
    private const string CURSOR = "|";        // Standard cursor character

    /// <summary>
    /// Initialize component and cache references.
    /// </summary>
    private void Awake()
    {
        textComponent = GetComponent<TextMeshProUGUI>();
        fullText = textComponent.text;  // Store initial text
        textComponent.text = "";        // Clear display until typing starts
    }

    /// <summary>
    /// Start typing automatically if the component is enabled at start.
    /// </summary>
    private void Start()
    {
        StartTyping();
    }

    /// <summary>
    /// Handle component re-enabling by restarting typing if needed.
    /// </summary>
    private void OnEnable()
    {
        // Restart typing if component was disabled and re-enabled
        if (typingCoroutine == null && !string.IsNullOrEmpty(fullText))
        {
            StartTyping();
        }
    }

    /// <summary>
    /// Clean up coroutines when component is disabled.
    /// </summary>
    private void OnDisable()
    {
        // Stop all running coroutines to prevent memory leaks
        StopAllCoroutines();
        typingCoroutine = null;
        cursorCoroutine = null;
    }

    /// <summary>
    /// Set new text to display and start typing animation.
    /// </summary>
    /// <param name="text">The text to display with typewriter effect</param>
    public void SetText(string text)
    {
        fullText = text;
        // Start typing immediately if component is active
        if (isActiveAndEnabled)
        {
            StartTyping();
        }
    }

    /// <summary>
    /// Begin the typewriter animation from the beginning.
    /// Stops any existing typing and starts fresh.
    /// </summary>
    public void StartTyping()
    {
        // Stop any existing typing animation
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }
        if (cursorCoroutine != null)
        {
            StopCoroutine(cursorCoroutine);
        }
        
        // Reset state and start typing
        textComponent.text = "";
        isTyping = true;
        typingCoroutine = StartCoroutine(TypeText());
    }

    /// <summary>
    /// Immediately complete the typing animation and display full text.
    /// Useful for skipping dialogue or fast-forwarding text.
    /// </summary>
    public void SkipTyping()
    {
        // Stop all running coroutines
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        if (cursorCoroutine != null)
        {
            StopCoroutine(cursorCoroutine);
            cursorCoroutine = null;
        }
        
        // Display complete text immediately
        textComponent.text = fullText;
        isTyping = false;
        OnTypingComplete?.Invoke();  // Notify listeners that typing is complete
    }

    /// <summary>
    /// Main coroutine that handles the character-by-character typing animation.
    /// Includes audio feedback, cursor blinking, and punctuation delays.
    /// </summary>
    private IEnumerator TypeText()
    {
        isTyping = true;
        textComponent.text = "";
        float delay = 1f / charactersPerSecond;  // Calculate delay between characters

        // Start cursor blinking animation if enabled
        if (enableCursor)
        {
            cursorCoroutine = StartCoroutine(BlinkCursor());
        }

        // Wait before starting to type (for dramatic effect)
        yield return useUnscaledTime ? new WaitForSecondsRealtime(startDelay) : new WaitForSeconds(startDelay);

        // Reset visible character counter
        typedVisibleCharCount = 0;

        // Type each character one by one
        for (int i = 0; i < fullText.Length; i++)
        {
            // Temporarily stop cursor while adding new character
            if (enableCursor && cursorCoroutine != null)
            {
                StopCoroutine(cursorCoroutine);
                cursorVisible = true;
            }

            // Add the next character to the display
            textComponent.text = fullText.Substring(0, i + 1);

            // Count visible (non-whitespace) characters and play sound every 2nd one
            if (!char.IsWhiteSpace(fullText[i]))
            {
                typedVisibleCharCount++;

                if (typedVisibleCharCount % 2 == 0 && audioSource != null && typeSound != null)
                {
                    // Vary pitch for more natural sound
                    audioSource.pitch = Random.Range(minPitch, maxPitch);
                    audioSource.PlayOneShot(typeSound);
                }
            }

            // Resume cursor blinking
            if (enableCursor)
            {
                cursorCoroutine = StartCoroutine(BlinkCursor());
            }

            // Apply extra delay for punctuation marks (natural reading pause)
            if (enablePunctuationDelays && i < fullText.Length - 1 && IsPunctuation(fullText[i]))
            {
                yield return useUnscaledTime ? new WaitForSecondsRealtime(punctuationDelay) : new WaitForSeconds(punctuationDelay);
            }
            else
            {
                // Standard delay between characters
                yield return useUnscaledTime ? new WaitForSecondsRealtime(delay) : new WaitForSeconds(delay);
            }
        }

        // Clean up cursor and finalize
        if (cursorCoroutine != null)
        {
            StopCoroutine(cursorCoroutine);
        }
        textComponent.text = fullText;  // Ensure complete text is displayed
        isTyping = false;
        OnTypingComplete?.Invoke();  // Notify completion
    }

    /// <summary>
    /// Handles the blinking cursor effect during typing.
    /// Alternates between showing and hiding a cursor character.
    /// </summary>
    private IEnumerator BlinkCursor()
    {
        while (true)  // Continue indefinitely until stopped
        {
            if (cursorVisible)
            {
                // Add cursor to end of current text
                textComponent.text = textComponent.text + CURSOR;
            }
            else
            {
                // Remove cursor if it's at the end
                if (textComponent.text.Length > 0 && textComponent.text[textComponent.text.Length - 1].ToString() == CURSOR)
                {
                    textComponent.text = textComponent.text.Substring(0, textComponent.text.Length - 1);
                }
            }
            cursorVisible = !cursorVisible;  // Toggle visibility
            yield return useUnscaledTime ? new WaitForSecondsRealtime(cursorBlinkRate) : new WaitForSeconds(cursorBlinkRate);
        }
    }

    /// <summary>
    /// Determines if a character should trigger punctuation delay.
    /// Includes common punctuation that naturally create reading pauses.
    /// </summary>
    /// <param name="character">Character to check</param>
    /// <returns>True if character is punctuation</returns>
    private bool IsPunctuation(char character)
    {
        return character == '.' || character == '!' || character == '?' ||  // End of sentence
               character == ',' || character == ';' || character == ':' ||  // Clause separators
               character == '-';  // Hyphen/dash
    }
}