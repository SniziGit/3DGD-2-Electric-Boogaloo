using UnityEngine;

public class PlayerDetector : MonoBehaviour
{
    public int playerCount = 0;
    public Material material;
    private void Start()
    {
        QuantityCheck();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerCount++;
            QuantityCheck();
        }

    }
    public void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerCount--;
            QuantityCheck();
        }
    }

    private void QuantityCheck()
    {
        if (playerCount == 0)
        {
            material.color = Color.red;
        }
        else if (playerCount == 1)
        {
            material.color = Color.yellow;
        }
        else if (playerCount >= 2)
        {
            material.color = Color.green;
        }
    }
}
