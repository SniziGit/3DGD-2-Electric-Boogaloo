using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class FadeIn : MonoBehaviour
{
    [Header("Canvas Group Settings")]
    public CanvasGroup canvasGroupToFade;
    public float fadeDuration = 1.0f;
    public float delayBeforeFade = 1f;
    
    [Header("Text Fading")]
    public TextMeshProUGUI[] textsToFade;
    public bool fadeTexts = true;
    public float textFadeInDuration = 0.5f; 
    public float textVisibleDuration = 2f; // How long text stays fully visible
    public float textFadeOutDuration = 1f; 
    
    [Header("Trigger Settings")]
    public bool fadeOnStart = true;
    public bool disableAfterFade = true;

    void Start()
    {
        foreach (var text in textsToFade)
        {
            if (text != null)
            {
                Color textColor = text.color;
                textColor.a = 0f;
                text.color = textColor;
            }
        }

        if (fadeOnStart)
        {
            StartCoroutine(FadeOutAndDisable());
        }
    }

    public IEnumerator FadeOutAndDisable()
    {
        // Wait for the specified delay
        yield return new WaitForSeconds(delayBeforeFade);
        
        // Start with canvas fully visible
        if (canvasGroupToFade != null)
        {
            canvasGroupToFade.alpha = 1f;
            canvasGroupToFade.interactable = true;
            canvasGroupToFade.blocksRaycasts = true;
            
            // Initialize texts to transparent (0 alpha)
            if (fadeTexts && textsToFade != null)
            {
                foreach (var text in textsToFade)
                {
                    if (text != null)
                    {
                        Color textColor = text.color;
                        textColor.a = 0f;
                        text.color = textColor;
                    }
                }
            }
            
            // Start the fade sequence
            StartCoroutine(FadeSequence());
        }
    }
    
    private IEnumerator FadeSequence()
    {
        // Text fade in immediately
        if (fadeTexts && textsToFade != null)
        {
            yield return StartCoroutine(FadeTextsIn());
            
            // Keep text visible for specified duration
            yield return new WaitForSeconds(textVisibleDuration);
        }
        
        // Fade canvas out first
        yield return StartCoroutine(FadeCanvasOut());
        
        // THEN fade text out
        if (fadeTexts && textsToFade != null)
        {
            yield return StartCoroutine(FadeTextsOut());
        }
        
        // Disable canvas in inspector if needed
        if (disableAfterFade)
        {
            canvasGroupToFade.gameObject.SetActive(false);
        }
    }
    
    private IEnumerator FadeTextsIn()
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < textFadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsedTime / textFadeInDuration);
            
            foreach (var text in textsToFade)
            {
                if (text != null)
                {
                    Color textColor = text.color;
                    textColor.a = alpha;
                    text.color = textColor;
                }
            }
            
            yield return null;
        }
        
        // Ensure final alpha is 1
        foreach (var text in textsToFade)
        {
            if (text != null)
            {
                Color textColor = text.color;
                textColor.a = 1f;
                text.color = textColor;
            }
        }
    }
    
    private IEnumerator FadeTextsOut()
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < textFadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / textFadeOutDuration);
            
            foreach (var text in textsToFade)
            {
                if (text != null)
                {
                    Color textColor = text.color;
                    textColor.a = alpha;
                    text.color = textColor;
                }
            }
            
            yield return null;
        }
        
        // Ensure final alpha is 0 and disable text objects
        foreach (var text in textsToFade)
        {
            if (text != null)
            {
                Color textColor = text.color;
                textColor.a = 0f;
                text.color = textColor;
                text.gameObject.SetActive(false); // Disable the text object
            }
        }
    }
    
    private IEnumerator FadeCanvasOut()
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
            
            if (canvasGroupToFade != null)
            {
                canvasGroupToFade.alpha = alpha;
                canvasGroupToFade.interactable = alpha > 0.1f;
                canvasGroupToFade.blocksRaycasts = alpha > 0.1f;
            }
            
            yield return null;
        }
        
        // Ensure final values
        if (canvasGroupToFade != null)
        {
            canvasGroupToFade.alpha = 0f;
            canvasGroupToFade.interactable = false;
            canvasGroupToFade.blocksRaycasts = false;
        }
    }
    
    public void TriggerFade()
    {
        StartCoroutine(FadeOutAndDisable());
    }
}
