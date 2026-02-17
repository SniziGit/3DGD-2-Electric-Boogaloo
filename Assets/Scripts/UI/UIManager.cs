using UnityEngine;

public class UIManager : MonoBehaviour
{
    public GameObject hitUIPrefab;
    
    [Header("Hit Effect Settings")]
    [SerializeField] private float fadeInDuration = 0.1f;
    [SerializeField] private float fadeOutDuration = 0.4f;
    [SerializeField] private float maxAlpha = 0.3f;
    
    private GameObject hitUIInstance;
    private CanvasGroup canvasGroup;
    private bool isFading = false;
    private float fadeTimer = 0f;
    private bool isFadingIn = false;

    private void Awake()
    {
        // Create persistent HitUI instance
        CreateHitUIInstance();
    }

    private void CreateHitUIInstance()
    {
        if (hitUIPrefab == null)
        {
            Debug.LogError("[UIManager] HitUI prefab is NULL!");
            return;
        }
        
        hitUIInstance = Instantiate(hitUIPrefab, transform);
        canvasGroup = hitUIInstance.GetComponent<CanvasGroup>();
        
        // Remove DestroyAfterTime component if it exists
        DestroyAfterTime destroyScript = hitUIInstance.GetComponent<DestroyAfterTime>();
        if (destroyScript != null)
        {
            Destroy(destroyScript);
        }
        
        // Start with disabled and transparent
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            hitUIInstance.SetActive(false);
        }
        
        Debug.Log($"[UIManager] HitUI instance created and disabled: {hitUIInstance.name}");
    }

    private void Update()
    {
        if (isFading && canvasGroup != null)
        {
            fadeTimer += Time.deltaTime;
            
            float duration = isFadingIn ? fadeInDuration : fadeOutDuration;
            float normalizedTime = fadeTimer / duration;
            
            if (isFadingIn)
            {
                // Fade in: 0 to maxAlpha
                canvasGroup.alpha = Mathf.Lerp(0f, maxAlpha, normalizedTime);
                
                if (fadeTimer >= fadeInDuration)
                {
                    // Start fade out
                    isFadingIn = false;
                    fadeTimer = 0f;
                }
            }
            else
            {
                // Fade out: maxAlpha to 0
                canvasGroup.alpha = Mathf.Lerp(maxAlpha, 0f, normalizedTime);
                
                if (fadeTimer >= fadeOutDuration)
                {
                    // Disable and stop fading
                    canvasGroup.alpha = 0f;
                    hitUIInstance.SetActive(false);
                    isFading = false;
                    fadeTimer = 0f;
                }
            }
        }
    }

    public void InstantiateHitUI()
    {
        Debug.Log("[UIManager] Triggering hit fade effect");
        
        if (canvasGroup == null)
        {
            Debug.LogError("[UIManager] CanvasGroup component is NULL!");
            return;
        }
        
        // Enable and start fade in
        hitUIInstance.SetActive(true);
        canvasGroup.alpha = 0f; // Ensure opacity is 0 first
        
        isFading = true;
        isFadingIn = true;
        fadeTimer = 0f;
        
        Debug.Log("[UIManager] Hit fade effect triggered!");
    }
}
