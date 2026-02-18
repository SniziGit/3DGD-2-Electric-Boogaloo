using UnityEngine;

public class Collectable : MonoBehaviour, IInteractable
{
    [Header("Collectable Settings")]
    [SerializeField] private string collectableName = "Energy Core";
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private float rotationSpeed = 50f;
    [SerializeField] private float floatAmplitude = 0.5f;
    [SerializeField] private float floatFrequency = 2f;
    
    [Header("Effects")]
    [SerializeField] private GameObject collectEffect;
    [SerializeField] private AudioClip collectSound;
    [SerializeField] private Light glowLight;
    
    private Vector3 startPosition;
    private float floatTimer;
    private bool isCollected = false;
    
    public string GetInteractionName()
    {
        return "Collect " + collectableName;
    }
    
    public bool CanInteract(GameObject player)
    {
        return !isCollected;
    }
    
    public void Interact(GameObject player)
    {
        if (isCollected)
            return;
        
        // Mark as collected
        isCollected = true;
        
        // Add to global collection manager
        if (CollectableManager.Instance != null)
        {
            CollectableManager.Instance.CollectCollectable(player);
        }
        else
        {
            Debug.LogWarning("CollectableManager not found in scene!");
        }
        
        // Play effects
        PlayCollectEffects();
        
        // Destroy the collectable
        Destroy(gameObject);
    }
    
    public float GetInteractionRange()
    {
        return interactionRange;
    }
    
    void Start()
    {
        startPosition = transform.position;
        
        // Ensure the object has the Pickup tag for interaction system
        if (gameObject.tag != "Pickup")
        {
            gameObject.tag = "Pickup";
        }
        
        // Setup glow light if not assigned
        if (glowLight == null)
        {
            glowLight = GetComponent<Light>();
            if (glowLight == null)
            {
                // Add a light component for visual feedback
                glowLight = gameObject.AddComponent<Light>();
                glowLight.type = LightType.Point;
                glowLight.range = 3f;
                glowLight.intensity = 2f;
                glowLight.color = Color.cyan;
            }
        }
    }
    
    void Update()
    {
        if (isCollected)
            return;
        
        // Rotate the collectable
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        
        // Float up and down
        floatTimer += Time.deltaTime * floatFrequency;
        Vector3 newPosition = startPosition;
        newPosition.y += Mathf.Sin(floatTimer) * floatAmplitude;
        transform.position = newPosition;
    }
    
    void PlayCollectEffects()
    {
        // Spawn collect effect
        if (collectEffect != null)
        {
            GameObject effect = Instantiate(collectEffect, transform.position, Quaternion.identity);
            Destroy(effect, 3f); // Destroy effect after 3 seconds
        }
        
        // Play collect sound
        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, transform.position);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw interaction range
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
