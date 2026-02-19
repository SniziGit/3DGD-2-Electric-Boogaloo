using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using TMPro;

public class DirectionalSequenceUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject sequencePanel;
    [SerializeField] private Image panelBackground;
    [SerializeField] private Transform[] arrowSlots; // Array of Image transforms for arrows
    [SerializeField] private Sprite upArrowSprite;
    [SerializeField] private Sprite downArrowSprite;
    [SerializeField] private Sprite leftArrowSprite;
    [SerializeField] private Sprite rightArrowSprite;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI timerText;
    
    [Header("Colors")]
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color fadeColor = new Color(0.3f, 0.3f, 0.3f, 0.5f); // Lower alpha gray
    [SerializeField] private Color successFlashColor = Color.cyan;
    [SerializeField] private Color failFlashColor = new Color(1f, 0.2f, 0.2f, 0.8f); // Bright red with lower alpha
    [SerializeField] private float flashDuration = 0.3f;
    
    private List<PasswordNode.Direction> currentSequence;
    private System.Action onCompleteCallback;
    private Color panelOriginalColor;
    
    private void Start()
    {
        if (sequencePanel != null)
            sequencePanel.SetActive(false);
            
        if (statusText != null)
            statusText.gameObject.SetActive(false);

        if (panelBackground != null)
            panelOriginalColor = panelBackground.color;
    }

    public void ShowPanel()
    {
        if (sequencePanel != null)
            sequencePanel.SetActive(true);

        if (panelBackground != null)
            panelBackground.color = panelOriginalColor;
    }

    public void HidePanel()
    {
        if (sequencePanel != null)
            sequencePanel.SetActive(false);

        if (statusText != null)
            statusText.gameObject.SetActive(false);

        if (timerText != null)
            timerText.text = "";
    }
    
    public void ShowSequence(List<PasswordNode.Direction> sequence, float displayTime, System.Action onComplete)
    {
        currentSequence = sequence;
        onCompleteCallback = onComplete;
        
        ShowPanel();
        
        // Display the arrows
        DisplayArrows(sequence);

        // No memorization countdown; input should start immediately.
        if (timerText != null)
            timerText.text = "";
    }
    
    private void DisplayArrows(List<PasswordNode.Direction> sequence)
    {
        for (int i = 0; i < arrowSlots.Length && i < sequence.Count; i++)
        {
            Image arrowImage = arrowSlots[i].GetComponent<Image>();
            if (arrowImage != null)
            {
                arrowImage.sprite = GetArrowSprite(sequence[i]);
                // Start in faded state; correct inputs will turn slots back to activeColor.
                arrowImage.color = fadeColor;
                arrowImage.gameObject.SetActive(true);
            }
        }
        
        // Hide extra slots
        for (int i = sequence.Count; i < arrowSlots.Length; i++)
        {
            if (arrowSlots[i] != null)
                arrowSlots[i].gameObject.SetActive(false);
        }
    }
    
    private Sprite GetArrowSprite(PasswordNode.Direction direction)
    {
        switch (direction)
        {
            case PasswordNode.Direction.Up:
                return upArrowSprite;
            case PasswordNode.Direction.Down:
                return downArrowSprite;
            case PasswordNode.Direction.Left:
                return leftArrowSprite;
            case PasswordNode.Direction.Right:
                return rightArrowSprite;
            default:
                return upArrowSprite;
        }
    }
    
    public void ShowInputPrompt()
    {
        if (statusText != null)
        {
            statusText.text = "Input the sequence!";
            statusText.color = Color.white;
            statusText.gameObject.SetActive(true);
        }
    }
    
    public void ShowInputFeedback(int currentIndex, bool correct)
    {
        if (currentIndex >= 0 && currentIndex < arrowSlots.Length && currentIndex < currentSequence.Count)
        {
            Image arrowImage = arrowSlots[currentIndex].GetComponent<Image>();
            if (arrowImage != null)
            {
                // Correct input turns the slot back to activeColor (white). Wrong stays red.
                arrowImage.color = correct ? activeColor : Color.red;
            }
        }
    }
    
    public void ShowResult(bool success)
    {
        if (statusText != null)
        {
            statusText.text = success ? "Correct! Door Unlocked!" : "Wrong! Try Again!";
            statusText.color = success ? Color.green : Color.red;
        }

        HidePanel();
        onCompleteCallback?.Invoke();
    }


    private void SetSlotsColor(Color color)
    {
        if (currentSequence == null)
            return;

        for (int i = 0; i < currentSequence.Count && i < arrowSlots.Length; i++)
        {
            Image arrowImage = arrowSlots[i].GetComponent<Image>();
            if (arrowImage != null)
                arrowImage.color = color;
        }
    }

    private IEnumerator FlashPanel(Color flashColor)
    {
        if (panelBackground == null)
            yield break;

        panelBackground.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        panelBackground.color = panelOriginalColor;
    }
    
    private IEnumerator HidePanelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        HidePanel();
            
        onCompleteCallback?.Invoke();
    }
    
    public void HideImmediately()
    {
        HidePanel();
    }
}
