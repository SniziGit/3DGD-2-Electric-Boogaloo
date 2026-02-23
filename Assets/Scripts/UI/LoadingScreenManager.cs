using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LoadingSceneManager : MonoBehaviour
{
    public static LoadingSceneManager Instance;
    public GameObject m_LoadingSceneObject;
    public Image ProgressBar;

    public CanvasGroup fadeIn;
    public float transitionTime;

    private Canvas canvas;

    [Header("Loading Text")]
    public TMP_Text loadingDotsText;
    public float dotsSpeed = 0.5f;

    [Header("Percentage Text")]
    public TMP_Text percentageText;

    [Header("Spinning Image")]
    public Image spinningImage;
    public float rotationSpeed = 180f; // Degrees per second
    public bool rotateClockwise = true;

    [Header("Fade Out Settings")]
    public CanvasGroup fadeOutGroup; // The new group to fade
    public float fadeOutDelayBefore = 0.5f; // Wait before fading
    public float fadeOutDuration = 1f;       // Fade duration
    public float fadeOutDelayAfter = 0.5f;   // Wait after fade

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(this.gameObject);

        // Get the Canvas component and set it to be screen space overlay and sort order high
        canvas = GetComponentInChildren<Canvas>(true);
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 9999; // Ensure it renders on top
            canvas.sortingLayerName = "UI";
        }

        // Make sure the loading screen is initially inactive
        if (m_LoadingSceneObject != null)
        {
            m_LoadingSceneObject.SetActive(false);
        }

        fadeIn.alpha = 0f;
    }

    private void Update()
    {
        // Rotate the spinning image if it's assigned and the loading screen is active
        if (spinningImage != null && m_LoadingSceneObject != null && m_LoadingSceneObject.activeInHierarchy)
        {
            float rotationDirection = rotateClockwise ? 1f : -1f;
            spinningImage.transform.Rotate(0f, 0f, rotationSpeed * rotationDirection * Time.unscaledDeltaTime);
        }
    }

    public void SwitchToScene(string id)
    {
        // Ensure timescale is normal during loading
        Time.timeScale = 1f;

        if (m_LoadingSceneObject != null)
        {
            // Make sure the canvas is properly set up before activating
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 9999;
            }

            m_LoadingSceneObject.SetActive(true);
            // Force update the canvas to prevent one frame delay
            Canvas.ForceUpdateCanvases();

            StartCoroutine(LoadingDotsRoutine());
        }

        if (ProgressBar != null)
        {
            ProgressBar.fillAmount = 0;
        }

        if (percentageText != null)
        {
            percentageText.text = "0%";
        }

        // Start sequence coroutine
        StartCoroutine(FadeThenLoad(id));
    }

    // New sequence fade first then start loading
    IEnumerator FadeThenLoad(string id)
    {
        // Fade CanvasGroup first
        yield return StartCoroutine(FadeCanvasGroup(fadeIn, 0f, 1f, transitionTime));

        // After fade completes, begin loading
        yield return StartCoroutine(SwitchToSceneAsync(id));
    }

    IEnumerator SwitchToSceneAsync(string id)
    {
        // Ensure timescale is normal during loading
        Time.timeScale = 1f;

        // Make sure the loading screen is active before starting the async operation
        if (m_LoadingSceneObject != null && !m_LoadingSceneObject.activeInHierarchy)
        {
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 9999;
            }
            m_LoadingSceneObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
        }

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(id);
        asyncLoad.allowSceneActivation = false;

        float target = 0f;
        float fillSpeed = 0.3f;

        // Slider fills only AFTER fade is done 
        while (asyncLoad.progress < 0.9f)
        {
            target = asyncLoad.progress;
            if (ProgressBar != null)
            {
                ProgressBar.fillAmount = Mathf.MoveTowards(ProgressBar.fillAmount, target, fillSpeed * Time.unscaledDeltaTime);
            }

            if (percentageText != null && ProgressBar != null)
            {
                int percent = Mathf.RoundToInt(ProgressBar.fillAmount * 100f);
                percentageText.text = percent + "%";
            }

            yield return null;
        }

        // Complete the progress bar
        if (ProgressBar != null)
        {
            while (ProgressBar.fillAmount < 1f)
            {
                ProgressBar.fillAmount = Mathf.MoveTowards(ProgressBar.fillAmount, 1f, fillSpeed * Time.unscaledDeltaTime);

                if (percentageText != null)
                {
                    int percent = Mathf.RoundToInt(ProgressBar.fillAmount * 100f);
                    percentageText.text = percent + "%";
                }

                yield return null;
            }
        }

        // Fade out the UI in CanvasGroup after loading is done
        if (fadeOutGroup != null)
        {
            // Optional wait before fade
            yield return new WaitForSecondsRealtime(fadeOutDelayBefore);

            // Fade out the additional CanvasGroup
            yield return StartCoroutine(FadeCanvasGroup(fadeOutGroup, 1f, 0f, fadeOutDuration));

            // Optional wait after fade
            yield return new WaitForSecondsRealtime(fadeOutDelayAfter);
        }

        // Small delay to show the completed progress bar
        yield return new WaitForSecondsRealtime(0.5f);

        // Allow the scene to activate
        asyncLoad.allowSceneActivation = true;

        // Wait for the scene to fully load
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // Small delay after scene load to ensure everything is ready
        yield return new WaitForSecondsRealtime(transitionTime);

        // Deactivate the loading screen
        if (m_LoadingSceneObject != null)
        {
            m_LoadingSceneObject.SetActive(false);
        }
    }

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end, float duration)
    {
        // Ensure the canvas is properly set up for the fade
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 9999;
        }

        float elapsed = 0f;
        cg.alpha = start;

        while (elapsed < duration)
        {
            // Ensure we're still the top canvas
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 9999;
            }

            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(start, end, elapsed / duration);
            yield return null;
        }

        cg.alpha = end;
    }

    IEnumerator LoadingDotsRoutine()
    {
        int dotCount = 0;

        while (m_LoadingSceneObject != null && m_LoadingSceneObject.activeInHierarchy)
        {
            dotCount = (dotCount + 1) % 4;

            if (loadingDotsText != null)
            {
                loadingDotsText.text = "Loading" + new string('.', dotCount);
            }

            yield return new WaitForSecondsRealtime(dotsSpeed);
        }
    }

    // Ensure the canvas is always properly set when enabled
    private void OnEnable()
    {
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 9999;
            Canvas.ForceUpdateCanvases();
        }
    }
}
