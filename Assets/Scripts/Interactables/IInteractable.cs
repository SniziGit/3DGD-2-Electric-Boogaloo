using UnityEngine;

public interface IInteractable
{
    string GetInteractionName();
    bool CanInteract(GameObject player);
    void Interact(GameObject player);
    float GetInteractionRange();
}
