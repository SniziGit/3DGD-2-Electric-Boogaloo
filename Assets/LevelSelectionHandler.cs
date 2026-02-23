using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelectionHandler : MonoBehaviour
{
    public void DifficultyHandler(int levelIndex)
    {
        GameManager.Instance.DifficultySetting(levelIndex);
    }
    
    public void Play()
    {
        if (LoadingSceneManager.Instance != null)
        {
            LoadingSceneManager.Instance.SwitchToScene("Carpet");
        }
        else
        {
            // Fallback to direct scene loading
            SceneManager.LoadScene("Carpet");
        }
    }
}
