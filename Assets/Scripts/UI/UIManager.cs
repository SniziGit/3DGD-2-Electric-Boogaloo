using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    public GameObject hitUIPrefab;

    private void Awake()
    {
        Instance = this;
    }

    public void InstantiateHitUI()
    {
        Instantiate(hitUIPrefab, transform);
    }
}
