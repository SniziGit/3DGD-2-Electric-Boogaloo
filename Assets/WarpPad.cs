using UnityEngine;
using UnityEngine.SceneManagement;

public class WarpPad : MonoBehaviour
{
    public PlayerDetector playerDetector;
    public string mainMenuSceneName = "MainMenu";
    public float delayBeforeLoad = 2f;
    
    private bool gameEnded = false;
    
    void Start()
    {
        if (playerDetector == null)
        {
            playerDetector = GetComponentInChildren<PlayerDetector>();
        }
        
        if (playerDetector == null)
        {
            Debug.LogError("WarpPad: PlayerDetector component not found!");
        }
    }

    void Update()
    {
        if (!gameEnded && playerDetector != null && playerDetector.playerCount >= 2)
        {
            EndGame();
        }
    }
    
    private void EndGame()
    {
        gameEnded = true;
        Debug.Log("Game Complete! 2 players reached the WarpPad!");
        
        Invoke("LoadMainMenu", delayBeforeLoad);
    }
    
    private void LoadMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
