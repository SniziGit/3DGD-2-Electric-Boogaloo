using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Timer : MonoBehaviour
{
    public static Timer Instance { get; private set; }
    public float currentTime;
    public float startingTime = 10f;
    public float startDelay = 1f; // Delay before timer starts counting down
    //LevelManager levelManager;

    [SerializeField] TextMeshProUGUI countdownText;

    private bool isPaused = false;
    private bool hasStarted = false; // Track if timer has started counting down
    public bool IsPaused => isPaused;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        currentTime = startingTime;
        //levelManager = FindFirstObjectByType<LevelManager>(); // get reference to level manager
        UpdateTimerDisplay(); // Show full time immediately

        // Start the delay coroutine
        StartCoroutine(StartTimerWithDelay());
    }

    // Update is called once per frame
    void Update()
    {
        if (isPaused || !hasStarted) return; // Don't count down if paused or haven't started yet

        if (currentTime > 0)
        {
            currentTime -= Time.deltaTime;
            if (currentTime < 0) currentTime = 0; // Prevent negative values
            UpdateTimerDisplay();
        }
        else
        {
            currentTime = 0;
            UpdateTimerDisplay();
        }
    }

    public void ResetTimer()
    {
        currentTime = startingTime;
        hasStarted = false; // Reset started state
        UpdateTimerDisplay();

        // Restart the delay if timer was reset
        StartCoroutine(StartTimerWithDelay());
    }

    public void PauseTimer()
    {
        isPaused = true;
    }

    public void ResumeTimer()
    {
        isPaused = false;
    }

    // Update the countdown text display by minutes and seconds
    private void UpdateTimerDisplay()
    {
        int minutes = Mathf.FloorToInt(currentTime / 60);
        int seconds = Mathf.FloorToInt(currentTime % 60);
        countdownText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
    public float GetTimeElapsed()
    {
        return startingTime - currentTime;
    }

    public float GetTimeRemaining()
    {
        return currentTime;
    }

    private IEnumerator StartTimerWithDelay()
    {
        // Wait for the specified delay before starting the timer
        yield return new WaitForSeconds(startDelay);

        // Mark as started so countdown can begin
        hasStarted = true;
    }
}
