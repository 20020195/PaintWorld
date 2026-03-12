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

    public RectTransform toolbarPanelRect;
    public Button fillToolBtn;
    public Button brushToolBtn;
    public GameObject brushSettingsPanel;
    public UnityEngine.UI.Slider brushSizeSlider;

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

        if (fillToolBtn != null)
            fillToolBtn.onClick.AddListener(() => SetTool(PaintTool.Fill));

        if (brushToolBtn != null)
            brushToolBtn.onClick.AddListener(() => SetTool(PaintTool.Brush));

        if (brushSizeSlider != null && paintController != null)
        {
            brushSizeSlider.value = paintController.brushRadius;
            brushSizeSlider.onValueChanged.AddListener(paintController.SetBrushRadius);
        }

        // Default tool UI state
        UpdateToolUI(PaintTool.Fill);
    }

    void SetTool(PaintTool tool)
    {
        if (paintController != null)
        {
            paintController.SetTool(tool);
            UpdateToolUI(tool);
        }
    }

    void UpdateToolUI(PaintTool activeTool)
    {
        if (fillToolBtn != null)
            fillToolBtn.GetComponent<Image>().color = (activeTool == PaintTool.Fill) ? Color.white : new Color(0.6f, 0.6f, 0.6f);

        if (brushToolBtn != null)
            brushToolBtn.GetComponent<Image>().color = (activeTool == PaintTool.Brush) ? Color.white : new Color(0.6f, 0.6f, 0.6f);

        if (brushSettingsPanel != null)
            brushSettingsPanel.SetActive(activeTool == PaintTool.Brush);

        if (toolbarPanelRect != null)
        {
            // If Brush is selected, height is 320 to show slider. If Fill, height is 150 for buttons only.
            float targetHeight = (activeTool == PaintTool.Brush) ? 320f : 160f;
            toolbarPanelRect.sizeDelta = new Vector2(toolbarPanelRect.sizeDelta.x, targetHeight);
        }
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
