using UnityEngine;

public class AudioListenerManager : MonoBehaviour
{
    public static AudioListenerManager Instance;
    
    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        // Ensure only one AudioListener exists
        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        
        // Keep only the first AudioListener (usually on the main camera)
        for (int i = 1; i < listeners.Length; i++)
        {
            if (listeners[i] != null)
            {
                Destroy(listeners[i]);
            }
        }
        
        Debug.Log($"Audio Listener Manager: Found {listeners.Length} listeners, kept 1");
    }
}
