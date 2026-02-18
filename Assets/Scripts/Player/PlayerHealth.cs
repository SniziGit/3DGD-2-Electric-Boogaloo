using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth;
    [SerializeField] private bool isDowned = false;
    
    [Header("UI References")]
    [SerializeField] private Image healthFillImage;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private float fillSmoothSpeed = 5f;
    
    [Header("Hit Feedback Settings")]
    [SerializeField] private GameObject hitUIPrefab;
    [SerializeField] private float fadeInDuration = 0.1f;
    [SerializeField] private float fadeOutDuration = 0.4f;
    [SerializeField] private float maxAlpha = 0.3f;
    
    private float currentHealthFill;
    private float targetHealthFill;
    
    private GameObject hitUIInstance;
    private CanvasGroup canvasGroup;
    private bool isFading = false;
    private float fadeTimer = 0f;
    private bool isFadingIn = false;
    
    // Remove singleton pattern to support multiple players
    // public static PlayerHealth Instance;
    
    [Header("Effects")]
    public AudioClip hitSFX;

    // Getters for other scripts
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => (float)currentHealth / maxHealth;
    public bool IsFullHealth() => currentHealth >= maxHealth;
    public bool IsDead() => currentHealth <= 0;
    public bool IsDowned() => isDowned;

    void Awake()
    {
        // Remove singleton pattern to support multiple players
        // This allows multiple players in the scene
    }
    
    void Start()
    {
        currentHealth = maxHealth;
        currentHealthFill = 1f; // 100% = full health
        targetHealthFill = 1f;
        
        UpdateHealthUI();
        CreateHitUIInstance();
    }
    
    void Update()
    {
        UpdateHealthFill();
        UpdateHitFeedback();
    }
    
    private void CreateHitUIInstance()
    {
        if (hitUIPrefab == null)
        {
            Debug.LogError("[PlayerHealth] HitUI prefab is NULL!");
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
        
        Debug.Log($"[PlayerHealth] HitUI instance created and disabled: {hitUIInstance.name}");
    }
    
    private void UpdateHitFeedback()
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
    
    private void TriggerHitFeedback()
    {
        Debug.Log("[PlayerHealth] Triggering hit fade effect");
        
        if (canvasGroup == null)
        {
            Debug.LogError("[PlayerHealth] CanvasGroup component is NULL!");
            return;
        }
        
        // Enable and start fade in
        hitUIInstance.SetActive(true);
        canvasGroup.alpha = 0f; // Ensure opacity is 0 first
        
        isFading = true;
        isFadingIn = true;
        fadeTimer = 0f;
        
        Debug.Log("[PlayerHealth] Hit fade effect triggered!");
    }
    
    public void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Damage")
        {
            DecreaseHealth(10);
        }
    }

    private void DecreaseHealth(int decreaseAmount)
    {
        Debug.Log($"[PlayerHealth] Taking {decreaseAmount} damage. Current health: {currentHealth}");
        
        currentHealth = Mathf.Max(0, currentHealth - decreaseAmount);
        UpdateHealthFillTarget();
        
        // Get the FPSMovement component from this GameObject
        FPSMovement movement = GetComponent<FPSMovement>();
        if (movement != null)
        {
            movement.AddShake(0.1f, 0.25f); // Shake the camera when taking damage
        }
        
        // Trigger hit feedback directly
        TriggerHitFeedback();
        
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(hitSFX); // Play hit sound effect when taking damage
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        // Instead of dying, enter downed state
        isDowned = true;
        currentHealth = 0;
        UpdateHealthFillTarget();
        
        // Notify GameManager to check if all players are downed
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CheckPlayerDowned(this);
        }
        
        Debug.Log("Player is downed! Can be revived.");
    }
    
    public void TakeDamage(int damageAmount)
    {
        DecreaseHealth(damageAmount);
    }
    
    public void Heal(int healAmount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + healAmount);
        UpdateHealthFillTarget();
    }
    
    public void SetHealth(int healthAmount)
    {
        currentHealth = Mathf.Clamp(healthAmount, 0, maxHealth);
        UpdateHealthFillTarget();
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    void UpdateHealthFillTarget()
    {
        if (healthFillImage != null)
        {
            targetHealthFill = (float)currentHealth / maxHealth;
        }
    }
    
    void UpdateHealthFill()
    {
        if (healthFillImage != null)
        {
            currentHealthFill = Mathf.Lerp(currentHealthFill, targetHealthFill, Time.deltaTime * fillSmoothSpeed);
            healthFillImage.fillAmount = currentHealthFill;
        }
        
        UpdateHealthText();
    }
    
    void UpdateHealthText()
    {
        if (healthText != null)
        {
            // Show only current health as integer
            healthText.text = $"{currentHealth}";
        }
    }
    
    void UpdateHealthUI()
    {
        UpdateHealthFillTarget();
        UpdateHealthFill();
    }
    
    public void Revive(int reviveHealth)
    {
        isDowned = false;
        currentHealth = reviveHealth;
        UpdateHealthFillTarget();
        
        // Notify GameManager that player was revived
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayerRevived(this);
        }
        
        Debug.Log($"Player revived with {reviveHealth} health!");
    }
    
    public void ForceDeath()
    {
        // Called by GameManager when game should end
        Time.timeScale = 0f;
        Debug.Log("Game Over - All players are downed!");
    }
    
}
