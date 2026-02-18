using UnityEngine;

public class CorpseDespawner : MonoBehaviour
{
    Enemy enemyScript;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        enemyScript = GetComponent<Enemy>();
    }

    // Update is called once per frame
    void Update()
    {
        if (enemyScript.enabled == false)
        {
            Destroy(gameObject, 3f); // Destroy the corpse after 5 seconds
        }
    }
}
