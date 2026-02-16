using UnityEngine;
using System.Collections;

public class Enemy : MonoBehaviour
{
    public int health = 100;
    private Rigidbody rb;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Damage") ;
        {
            health -= 10;
            if (health <= 0)
            {
                Die();
            }
        }
    }

    void Die()
    {
        rb.freezeRotation = false;
        transform.rotation = Quaternion.Euler(transform.rotation.x, transform.rotation.y, transform.rotation.z + 5);
        this.enabled = false; // Disable enemy behavior
    }
}
