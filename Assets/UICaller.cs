using UnityEngine;
using TMPro;

public class UICaller : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI collectableUI;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        CollectableManager.Instance.AssignCollectableUI(collectableUI);
    }
}
