using UnityEngine;

public class DoorAnimTrigger : MonoBehaviour
{
    private Animator doorAnimator;
    private Collider doorCollider;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        doorAnimator = GetComponent<Animator>();
        doorCollider = GetComponent<Collider>();

    }

    private void OnTriggerStay(Collider other)
    {
        doorAnimator.SetBool("isOpen", true);
    }
}
