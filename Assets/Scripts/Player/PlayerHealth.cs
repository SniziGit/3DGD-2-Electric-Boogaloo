using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth;
    
    [Header("UI References")]
    [SerializeField] private Image healthFillImage;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private float fillSmoothSpeed = 5f;
    
    private float currentHealthFill;
    private float targetHealthFill;
    
    public static PlayerHealth Instance;
    
    [Header("Effects")]
    public AudioClip hitSFX;

    // Getters for other scripts
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => (float)currentHealth / maxHealth;
    public bool IsFullHealth() => currentHealth >= maxHealth;
    public bool IsDead() => currentHealth <= 0;

    void Awake()
    {
        // Singleton pattern for easy access
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
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
        currentHealth = Mathf.Max(0, currentHealth - decreaseAmount);
        UpdateHealthFillTarget();
        
        FPSMovement.Instance.AddShake(0.1f, 0.25f); // Shake the camera when taking damage
        UIManager.Instance.InstantiateHitUI(); // Show hit UI when taking damage
        AudioManager.Instance.PlaySFX(hitSFX); // Play hit sound effect when taking damage

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Time.timeScale = 0f;
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
    
}
