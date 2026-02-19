using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InteractionPreview : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject interactionPanel;
    [SerializeField] private TextMeshProUGUI interactionText;
    [SerializeField] private Image progressBar;
    [SerializeField] private TextMeshProUGUI progressText;

    [Header("Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private string[] interactableTags = { "Interactable", "Pickup" };
    [SerializeField] private float raycastDistance = 5f;

    private Camera playerCamera;
    private FPSMovement playerMovement;
    private ReviveInteraction reviveInteraction;
    private DoorInteraction doorInteraction;
    private PlayerHealth playerHealth;

    private IInteractable currentInteractable;
    private PlayerHealth currentReviveTarget;
    private GameObject currentTarget;

    private bool isReviving = false;
    private float reviveProgress = 0f;
    private bool isHacking = false;
    private float hackProgress = 0f;
    private bool isHoldingHack = false;
    private float hackHoldProgress = 0f;

    void Start()
    {
        playerCamera = GetComponentInChildren<Camera>();
        playerMovement = GetComponent<FPSMovement>();
        reviveInteraction = GetComponent<ReviveInteraction>();
        doorInteraction = GetComponent<DoorInteraction>();
        playerHealth = GetComponent<PlayerHealth>();

        if (interactionPanel != null)
            interactionPanel.SetActive(false);
    }

    void Update()
    {
        if (playerHealth != null && playerHealth.IsDowned())
        {
            HideInteractionUI();
            return;
        }

        CheckForInteractables();
        UpdateReviveProgress();
        UpdateHackProgress();
        UpdateUI();
    }

    void CheckForInteractables()
    {
        // Keep targets while reviving or hacking
        if ((reviveInteraction != null && reviveInteraction.IsReviving()) ||
            (doorInteraction != null && (doorInteraction.IsHacking() || doorInteraction.IsHoldingHack())))
            return;

        currentInteractable = null;
        currentReviveTarget = null;
        currentTarget = null;

        // --- REVIVE CHECK ---
        if (reviveInteraction != null && reviveInteraction.CanRevive())
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

            foreach (GameObject player in players)
            {
                if (player == gameObject)
                    continue;

                PlayerHealth targetHealth = player.GetComponent<PlayerHealth>();
                if (targetHealth != null && targetHealth.IsDowned())
                {
                    float distance = Vector3.Distance(transform.position, player.transform.position);
                    if (distance <= interactionRange)
                    {
                        Vector3 directionToTarget = (player.transform.position - transform.position).normalized;
                        float dot = Vector3.Dot(playerCamera.transform.forward, directionToTarget);

                        if (dot > 0.5f)
                        {
                            currentReviveTarget = targetHealth;
                            currentTarget = player;
                            return;
                        }
                    }
                }
            }
        }

        // --- DOOR HACK CHECK ---
        if (doorInteraction != null)
        {
            RaycastHit hit;
            if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, raycastDistance))
            {
                if (hit.distance <= interactionRange && hit.collider.CompareTag("Door"))
                {
                    currentTarget = hit.collider.gameObject;
                    return;
                }
            }
        }

        // --- NORMAL INTERACTABLE RAYCAST ---
        RaycastHit hit2;
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit2, raycastDistance))
        {
            if (hit2.distance <= interactionRange && IsInteractableTag(hit2.collider.tag))
            {
                IInteractable interactable = hit2.collider.GetComponent<IInteractable>();
                if (interactable != null)
                {
                    currentInteractable = interactable;
                    currentTarget = hit2.collider.gameObject;
                }
            }
        }

        // --- TAG-BASED FALLBACK ---
        foreach (string tag in interactableTags)
        {
            GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(tag);
            foreach (GameObject obj in taggedObjects)
            {
                float distance = Vector3.Distance(transform.position, obj.transform.position);
                if (distance <= interactionRange)
                {
                    Vector3 dir = (obj.transform.position - transform.position).normalized;
                    float dot = Vector3.Dot(playerCamera.transform.forward, dir);

                    if (dot > 0.5f)
                    {
                        IInteractable interactable = obj.GetComponent<IInteractable>();
                        if (interactable != null)
                        {
                            currentInteractable = interactable;
                            currentTarget = obj;
                            break;
                        }
                    }
                }
            }
        }
    }

    void UpdateReviveProgress()
    {
        if (reviveInteraction != null)
        {
            isReviving = reviveInteraction.IsReviving();
            reviveProgress = reviveInteraction.GetReviveProgress();
        }
        else
        {
            isReviving = false;
            reviveProgress = 0f;
        }
    }

    void UpdateHackProgress()
    {
        if (doorInteraction != null)
        {
            isHacking = doorInteraction.IsHacking();
            hackProgress = doorInteraction.GetHackProgress();
            isHoldingHack = doorInteraction.IsHoldingHack();
            hackHoldProgress = doorInteraction.GetHackHoldProgress();
        }
        else
        {
            isHacking = false;
            hackProgress = 0f;
            isHoldingHack = false;
            hackHoldProgress = 0f;
        }
    }

    void UpdateUI()
    {
        bool shouldShowUI = currentInteractable != null ||
                            currentReviveTarget != null ||
                            currentTarget != null ||
                            isReviving ||
                            isHacking ||
                            isHoldingHack;

        if (interactionPanel != null)
            interactionPanel.SetActive(shouldShowUI);

        if (!shouldShowUI || interactionText == null)
            return;

        // --- UI TEXT LOGIC ---
        if (isReviving)
        {
            interactionText.text = "Reviving...";
        }
        else if (isHoldingHack)
        {
            interactionText.text = $"Hacking...";
        }
        else if (isHacking)
        {
            interactionText.text = "Enter Password...";
        }
        else if (currentReviveTarget != null)
        {
            interactionText.text = "F to Revive";
        }
        else if (currentTarget != null && currentTarget.CompareTag("Door"))
        {
            float cooldownRemaining = 0f;
            if (doorInteraction != null)
            {
                DoorAnimTrigger door = currentTarget.GetComponentInParent<DoorAnimTrigger>();
                if (door == null)
                    door = currentTarget.GetComponent<DoorAnimTrigger>();

                if (door != null)
                    cooldownRemaining = doorInteraction.GetDoorCooldownRemaining(door);
            }

            if (cooldownRemaining > 0f)
            {
                interactionText.text = $"Hack again in {cooldownRemaining:F0}s";
            }
            else
            {
                interactionText.text = "F to Hack";
            }
        }
        else if (currentInteractable != null)
        {
            string itemName = currentInteractable.GetInteractionName();

            // --- Check for health/stamina full ---
            if (!currentInteractable.CanInteract(gameObject))
            {
                if (itemName.ToLower().Contains("health"))
                    interactionText.text = "Health Full";
                else if (itemName.ToLower().Contains("stamina"))
                    interactionText.text = "Stamina Full";
                else
                    interactionText.text = $"{itemName} Unavailable";
            }
            else
            {
                interactionText.text = $"F to {itemName}";
            }
        }

        // --- PROGRESS BAR ---
        if (progressBar != null)
        {
            bool showProgress = isReviving || isHacking || isHoldingHack;
            progressBar.gameObject.SetActive(showProgress);

            if (isReviving)
                progressBar.fillAmount = reviveProgress;
            else if (isHoldingHack)
                progressBar.fillAmount = hackHoldProgress;
            else if (isHacking)
                progressBar.fillAmount = hackProgress;
        }

        // --- PROGRESS TEXT ---
        if (progressText != null)
        {
            // Show progress text for revive and hacking hold
            bool showProgress = isReviving || isHoldingHack;
            progressText.gameObject.SetActive(showProgress);

            if (isReviving)
                progressText.text = $"{Mathf.RoundToInt(reviveProgress * 100)}%";
            else if (isHoldingHack)
                progressText.text = $"{Mathf.RoundToInt(hackHoldProgress * 100)}%";
        }
    }

    void HideInteractionUI()
    {
        if (interactionPanel != null)
            interactionPanel.SetActive(false);
    }

    bool IsInteractableTag(string tag)
    {
        foreach (string interactableTag in interactableTags)
        {
            if (tag == interactableTag)
                return true;
        }
        return false;
    }

    public bool HasInteractionTarget()
    {
        return currentInteractable != null || currentReviveTarget != null || currentTarget != null;
    }

    public IInteractable GetCurrentInteractable()
    {
        return currentInteractable;
    }

    public PlayerHealth GetCurrentReviveTarget()
    {
        return currentReviveTarget;
    }
}
