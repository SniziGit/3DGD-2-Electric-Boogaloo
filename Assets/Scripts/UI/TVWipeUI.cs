using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;


public class TVWipeUI : MonoBehaviour
{
    public RectTransform tvTransitionPanel;
    public float duration = 0.4f;
    public RectTransform startPoint;
    public Button fadeInButton;
    
    [Header("Wipe Settings")]
    public Vector3 horizontalScale = new Vector3(1f, 0.001f, 1f);

    [Header("Post TV Anim Movement")]
    public bool enablePostWipeMovement = false;
    public Vector2 postWipePosition = Vector2.zero;
    public float postWipeDelay = 0.5f;
    public float postWipeDuration = 0.3f;
    public Ease postWipeEase = Ease.InOutQuad;

    private Vector3 originalScale;
    private Vector2 originalPos;

    void Start()
    {
        // Store original values
        originalScale = tvTransitionPanel.localScale;
        originalPos = tvTransitionPanel.anchoredPosition;

        // Start collapsed
        tvTransitionPanel.localScale = Vector3.zero;

        // Don't override startPoint if it's already assigned in inspector
        if (startPoint == null)
        {
            startPoint = GetComponent<RectTransform>();
        }

        // Position at start point
        tvTransitionPanel.anchoredPosition = startPoint.anchoredPosition;
    }

    // ----------------------------------------------------
    // OPEN (plays fully horizontal, then vertical)
    // ----------------------------------------------------
    public void OpenWipe()
    {
        // Auto-enable GameObject if disabled
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }
        
        // Start from startPoint position
        tvTransitionPanel.anchoredPosition = startPoint.anchoredPosition;
        
        // Make button non-interactable immediately
        if (fadeInButton != null)
        {
            fadeInButton.interactable = false;
        }

        Sequence seq = DOTween.Sequence();

        // ---- Step 1: Horizontal wipe (X expands, Y stays thin)
        seq.Append(
            tvTransitionPanel.DOScale(
                horizontalScale,
                duration
            ).SetEase(Ease.InOutQuad)
        );

        seq.Join(
            tvTransitionPanel.DOAnchorPos(originalPos, duration)
        );

        // ---- Step 2: Vertical expansion AFTER step 1 completes
        seq.Append(
            tvTransitionPanel.DOScale(
                originalScale,
                duration
            ).SetEase(Ease.InOutQuad)
        );

        // ---- Step 3: Optional post-wipe movement with delay
        if (enablePostWipeMovement)
        {
            // Add delay before post-wipe movement
            seq.AppendInterval(postWipeDelay);
            
            // Move to post-wipe position
            seq.Append(
                tvTransitionPanel.DOAnchorPos(postWipePosition, postWipeDuration)
                    .SetEase(postWipeEase)
            );
        }

        seq.Play();
    }


    // ----------------------------------------------------
    // CLOSE
    // ----------------------------------------------------
    public void CloseWipe()
    {
        // Auto-enable GameObject if disabled
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }
        
        Sequence seq = DOTween.Sequence();

        seq.Append(
            tvTransitionPanel.DOScale(Vector3.zero, duration)
        );

        seq.Join(
            tvTransitionPanel.DOAnchorPos(startPoint.anchoredPosition, duration)
        );

        // Re-enable button and disable GameObject after the animation
        seq.OnComplete(() => {
            if (fadeInButton != null)
            {
                fadeInButton.interactable = true;
            }
            gameObject.SetActive(false);
        });

        seq.Play();
    }

}
