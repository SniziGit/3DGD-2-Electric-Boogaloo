using UnityEngine;

public class DoorAnimTrigger : MonoBehaviour
{
    private Animator doorAnimator;
    private Collider doorCollider;
    [SerializeField] private float closeDelay = 2.0f;
    private bool isPlayerInside = false;
    private Coroutine closeCoroutine;

    void Start()
    {
        doorAnimator = GetComponent<Animator>();
        doorCollider = GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = true;
            doorAnimator.SetBool("isOpen", true);
            
            if (closeCoroutine != null)
            {
                StopCoroutine(closeCoroutine);
                closeCoroutine = null;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = false;
            closeCoroutine = StartCoroutine(CloseDoorAfterDelay());
        }
    }

    private System.Collections.IEnumerator CloseDoorAfterDelay()
    {
        yield return new WaitForSeconds(closeDelay);
        
        if (!isPlayerInside)
        {
            doorAnimator.SetBool("isOpen", false);
        }
        closeCoroutine = null;
    }
}
