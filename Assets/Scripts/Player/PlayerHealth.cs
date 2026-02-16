using JetBrains.Annotations;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int health = 100;

    public AudioClip hitSFX;

    public void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Damage")
        {
            DecreaseHealth(10);
        }
    }

    private void DecreaseHealth(int decreaseAmount)
    {
        health -= decreaseAmount;
        FPSMovement.Instance.AddShake(0.1f, 0.25f); // Shake the camera when taking damage
        UIManager.Instance.InstantiateHitUI(); // Show hit UI when taking damage
        AudioManager.Instance.PlaySFX(hitSFX); // Play hit sound effect when taking damage

        if (health <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Time.timeScale = 0f;
    }   
}
