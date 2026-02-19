using UnityEngine;
using System.Collections.Generic;

public class Breakables : MonoBehaviour, IDamageable
{
    [Header("Breakable Settings")]
    public int health = 50;
    public float destroyDelay = 0.5f;
    public GameObject[] dropItems; // Array of items that can drop
    [Range(0f, 100f)]
    public float dropChance = 10f; // 10% chance to drop something
    public int minDrops = 1;
    public int maxDrops = 3;
    
    [Header("Effects")]
    public GameObject breakEffect;
    public AudioClip breakSound;
    
    private int currentHealth;
    private bool isDestroyed = false;
    
    void Start()
    {
        currentHealth = health;
        
        // Check if object is on the Breakable layer
        if (gameObject.layer != LayerMask.NameToLayer("Breakable"))
        {
            Debug.LogWarning($"{gameObject.name} is not on the Breakable layer! Please assign it to the Breakable layer for proper functionality.");
        }
    }
    
    public void TakeDamage(int damageAmount)
    {
        if (isDestroyed) return;
        
        currentHealth -= damageAmount;
        
        if (currentHealth <= 0)
        {
            BreakObject();
        }
    }
    
    void BreakObject()
    {
        if (isDestroyed) return;
        isDestroyed = true;
        
        // Play break effects
        if (breakEffect != null)
        {
            GameObject effect = Instantiate(breakEffect, transform.position, transform.rotation);
            // Destroy the VFX after it plays (assuming it has a particle system)
            ParticleSystem particles = effect.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                Destroy(effect, particles.main.duration + 1f);
            }
            else
            {
                Destroy(effect, 3f); // Fallback destroy time
            }
        }
        
        if (breakSound != null)
            AudioSource.PlayClipAtPoint(breakSound, transform.position);
        
        // Try to drop items
        TryDropItems();
        
        // Destroy the breakable object
        Destroy(gameObject, destroyDelay);
    }
    
    void TryDropItems()
    {
        if (dropItems == null || dropItems.Length == 0) return;
        
        // Check if we should drop anything
        if (Random.Range(0f, 100f) > dropChance) return;
        
        // Determine how many items to drop
        int dropCount = Random.Range(minDrops, maxDrops + 1);
        
        // Drop random items
        List<GameObject> droppedItems = new List<GameObject>();
        
        for (int i = 0; i < dropCount; i++)
        {
            // Pick random item from array
            GameObject randomItem = dropItems[Random.Range(0, dropItems.Length)];
            
            // Don't drop the same item twice in a row
            if (droppedItems.Contains(randomItem)) continue;
            
            // Instantiate the item at breakable position with small random offset
            Vector3 dropPosition = transform.position + new Vector3(
                Random.Range(-0.3f, 0.3f),
                0.1f, // Start just above ground
                Random.Range(-0.3f, 0.3f)
            );
            
            GameObject droppedItem = Instantiate(randomItem, dropPosition, Quaternion.identity);
            
            //// Add Rigidbody if it doesn't exist to make it fall
            //Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
            //if (rb == null)
            //{
            //    rb = droppedItem.AddComponent<Rigidbody>();
            //}
            
            //// Reset any existing angular velocity to prevent spinning
            //rb.angularVelocity = Vector3.zero;
            
            //// Add very gentle downward force
            //rb.AddForce(Vector3.down * 0.5f, ForceMode.Impulse);
            
            //// Add minimal horizontal force
            //rb.AddForce(new Vector3(
            //    Random.Range(-0.1f, 0.1f),
            //    0f,
            //    Random.Range(-0.1f, 0.1f)
            //), ForceMode.Impulse);
            
            //// Freeze rotation to prevent spinning
            //rb.freezeRotation = true;
            
            droppedItems.Add(randomItem);
            
            // Limit to prevent too many drops
            if (droppedItems.Count >= dropItems.Length) break;
        }
        
        Debug.Log($"Dropped {droppedItems.Count} items from {gameObject.name}");
    }
    
    // Get current health for UI or other systems
    public int GetCurrentHealth()
    {
        return currentHealth;
    }
    
    // Check if object is destroyed
    public bool IsDestroyed()
    {
        return isDestroyed;
    }
}
