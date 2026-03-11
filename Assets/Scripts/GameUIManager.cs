using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Manages the game UI: win screen, complete & back buttons.
/// Progress slider/text removed per design update.
/// </summary>
public class GameUIManager : MonoBehaviour
{
    [Header("Win Screen")]
    public GameObject winPanel;
    public Button playAgainButton;

    [Header("Top Bar")]
    public Button undoButton;
    public Button completeButton;
    public Button backButton;

    [Header("References")]
    public PaintController paintController;

    void Start()
    {
        if (winPanel != null)
            winPanel.SetActive(false);

        if (playAgainButton != null)
            playAgainButton.onClick.AddListener(OnPlayAgain);

        if (undoButton != null)
            undoButton.onClick.AddListener(OnUndoClicked);

        if (completeButton != null)
            completeButton.onClick.AddListener(OnCompleteClicked);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
    }

    // Called from PaintController — kept signature for compatibility
    public void UpdateProgress(int painted, int total) { /* no-op, removed UI */ }

    public void ShowWinScreen()
    {
        if (winPanel != null)
        {
            winPanel.SetActive(true);
            StartCoroutine(AnimateWinPanel());
        }
    }

    IEnumerator AnimateWinPanel()
    {
        if (winPanel == null) yield break;
        CanvasGroup cg = winPanel.GetComponent<CanvasGroup>();
        if (cg == null) cg = winPanel.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(t / 0.6f);
            yield return null;
        }
        cg.alpha = 1f;
    }

    void OnUndoClicked()
    {
        if (paintController != null) paintController.Undo();
    }

    void OnCompleteClicked() => ShowWinScreen();

    void OnPlayAgain()
    {
        if (winPanel != null) winPanel.SetActive(false);
        if (paintController != null) paintController.ResetPainting();
    }

    void OnBackClicked()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("PictureSelect");
    }
}
