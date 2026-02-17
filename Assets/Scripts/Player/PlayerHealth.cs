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
    [SerializeField] private UIManager uiManager;
    [SerializeField] private float fillSmoothSpeed = 5f;
    
    private float currentHealthFill;
    private float targetHealthFill;
    
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
    }
    
    void Update()
    {
        UpdateHealthFill();
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
        
        Debug.Log($"[PlayerHealth] UIManager reference: {(uiManager != null ? "FOUND" : "NULL")}");
        if (uiManager != null)
        {
            uiManager.InstantiateHitUI(); // Show hit UI when taking damage
            Debug.Log("[PlayerHealth] HitUI instantiated!");
        }
        else
        {
            Debug.LogWarning("[PlayerHealth] UIManager is NULL! HitUI cannot be shown.");
        }
        
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
