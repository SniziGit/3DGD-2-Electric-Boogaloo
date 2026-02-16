using UnityEngine;

public class Fly : MonoBehaviour
{
    [Header("Hover Settings")]
    [SerializeField] private float hoverAmplitude = 0.3f;   // distance up/down
    [SerializeField] private float hoverDuration = 2f;      // time to go up/down
    [SerializeField] private Transform hoverVisual;
    [SerializeField] private float hoverOffset = 2f;

    private float baseHeight;
    private float hoverTime;
    private Vector3 originalPosition;

    void Start()
    {
        Init();
    }

    private void Init()
    {
        // Store the original world position
        originalPosition = transform.position;
        baseHeight = originalPosition.y + hoverOffset;
        hoverTime = 0f;
    }

    void Update()
    {
        UpdateHover();
    }

    private void UpdateHover()
    {
        hoverTime += Time.deltaTime;
        
        // Use Mathf.Sin to create smooth up/down movement
        float hoverValue = Mathf.Sin((hoverTime / hoverDuration) * 2f * Mathf.PI) * hoverAmplitude;
        
        // Only modify the Y position, preserve X and Z from other movement systems
        Vector3 currentPosition = transform.position;
        currentPosition.y = baseHeight + hoverValue;
        
        // Move the entire GameObject including collider
        transform.position = currentPosition;
    }
}
