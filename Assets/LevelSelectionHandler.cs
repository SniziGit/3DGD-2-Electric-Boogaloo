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
        SceneManager.LoadScene("Carpet");


    }
}
