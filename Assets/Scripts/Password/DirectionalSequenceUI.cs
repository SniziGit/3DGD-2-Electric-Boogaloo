using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using TMPro;

public class DirectionalSequenceUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject sequencePanel;
    [SerializeField] private Transform[] arrowSlots;
    [SerializeField] private Sprite upArrowSprite;
    [SerializeField] private Sprite downArrowSprite;
    [SerializeField] private Sprite leftArrowSprite;
    [SerializeField] private Sprite rightArrowSprite;

    [Header("Colors")]
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color fadeColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

    [Header("Flicker Settings")]
    [SerializeField] private GameObject correctFlickerObject;
    [SerializeField] private GameObject wrongFlickerObject;
    [SerializeField] private float flickerDuration = 0.5f;
    [SerializeField] private int flickerCount = 3;

    private List<PasswordNode.Direction> currentSequence;
    private System.Action onCompleteCallback;

    private void Start()
    {
        if (sequencePanel != null)
            sequencePanel.SetActive(false);
    }

    public void ShowPanel()
    {
        if (sequencePanel != null)
            sequencePanel.SetActive(true);
    }

    public void HidePanel()
    {
        if (sequencePanel != null)
            sequencePanel.SetActive(false);
    }

    public void ShowSequence(List<PasswordNode.Direction> sequence, float displayTime, System.Action onComplete)
    {
        currentSequence = sequence;
        onCompleteCallback = onComplete;

        ShowPanel();
        DisplayArrows(sequence);
    }

    private void DisplayArrows(List<PasswordNode.Direction> sequence)
    {
        for (int i = 0; i < arrowSlots.Length && i < sequence.Count; i++)
        {
            Image arrowImage = arrowSlots[i].GetComponent<Image>();
            if (arrowImage != null)
            {
                arrowImage.sprite = GetArrowSprite(sequence[i]);
                arrowImage.color = fadeColor;
                arrowImage.gameObject.SetActive(true);
            }
        }

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
            case PasswordNode.Direction.Up: return upArrowSprite;
            case PasswordNode.Direction.Down: return downArrowSprite;
            case PasswordNode.Direction.Left: return leftArrowSprite;
            case PasswordNode.Direction.Right: return rightArrowSprite;
            default: return upArrowSprite;
        }
    }

    public void ShowInputFeedback(int currentIndex, bool correct)
    {
        if (currentIndex >= 0 && currentIndex < arrowSlots.Length && currentIndex < currentSequence.Count)
        {
            Image arrowImage = arrowSlots[currentIndex].GetComponent<Image>();
            if (arrowImage != null)
                arrowImage.color = correct ? activeColor : Color.red;
        }
    }

    public void ShowResult(bool success)
    {
        StopAllCoroutines();
        StartCoroutine(FlickerAndClose(success));
    }

    private IEnumerator FlickerAndClose(bool success)
    {
        GameObject flickerObject = success ? correctFlickerObject : wrongFlickerObject;

        if (flickerObject == null)
        {
            HidePanel();
            onCompleteCallback?.Invoke();
            yield break;
        }

        float singleFlashTime = flickerDuration / flickerCount;

        flickerObject.SetActive(false);

        for (int i = 0; i < flickerCount; i++)
        {
            flickerObject.SetActive(true);
            yield return new WaitForSecondsRealtime(singleFlashTime / 2f);

            flickerObject.SetActive(false);
            yield return new WaitForSecondsRealtime(singleFlashTime / 2f);
        }

        yield return new WaitForSecondsRealtime(0.2f);

        HidePanel();
        onCompleteCallback?.Invoke();
    }

    public void HideImmediately()
    {
        HidePanel();
    }
}
