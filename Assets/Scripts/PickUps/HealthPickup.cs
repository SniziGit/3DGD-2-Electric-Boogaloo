using UnityEngine;

public class HealthPickup : MonoBehaviour
{
    [Header("Health Pickup Settings")]
    public int healthAmount = 25;
    public float rotationSpeed = 50f;
    public float floatHeight = 0.3f; // Reduced height
    public float floatAmplitude = 0.1f; // Reduced amplitude for smoother movement
    
    private Vector3 startPosition;
    private float timeOffset;
    
    void Start()
    {
        startPosition = transform.position;
        timeOffset = Random.Range(0f, Mathf.PI * 2f);
    }
    
    void Update()
    {
        // Smoother floating animation using cosine for gentler movement
        float newY = startPosition.y + Mathf.Cos(Time.time * rotationSpeed * 0.5f + timeOffset) * floatAmplitude;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        
        // Slower rotation
        transform.Rotate(Vector3.up, rotationSpeed * 0.3f * Time.deltaTime, Space.World);
    }
    
    // This method can be called to override the health amount
    public void SetHealthAmount(int amount)
    {
        healthAmount = amount;
    }
    
    // Get the health amount this pickup provides
    public int GetHealthAmount()
    {
        return healthAmount;
    }
}
