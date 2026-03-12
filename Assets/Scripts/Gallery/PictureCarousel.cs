using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

/// <summary>
/// Horizontal carousel with snap-to-center behavior.
/// Center card is selected (full scale/alpha); others fade and shrink.
/// Click the center card to load PaintScene with the selected picture.
/// </summary>
public class PictureCarousel : MonoBehaviour, IDragHandler, IEndDragHandler, IBeginDragHandler
{
    [Header("References")]
    public RectTransform contentContainer;   // parent of all cards
    public Button selectButton;              // "Chọn tranh này" button
    public Text selectedNameText;            // shows name of center card

    [Header("Cards")]
    public List<PictureCard> cards = new List<PictureCard>();

    [Header("Layout")]
    public float cardSpacing = 520f;         // distance between card centers
    public float snapSpeed   = 10f;          // lerp speed for snap

    [Header("Select")]
    public string paintSceneName = "PaintScene";

    // ── internal state ────────────────────────────────────────────
    private int   currentIndex = 0;
    private float targetX      = 0f;
    private bool  isDragging   = false;
    private float dragStartX   = 0f;
    private float containerStartX = 0f;

    void Start()
    {
        if (cards.Count == 0) return;

        // Position cards
        for (int i = 0; i < cards.Count; i++)
        {
            RectTransform rt = cards[i].GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(i * cardSpacing, 0);
        }

        // Start at center (index 0)
        SnapToIndex(0, instant: true);

        if (selectButton != null)
            selectButton.onClick.AddListener(OnSelectClicked);

        UpdateCardVisuals();
        UpdateSelectionUI();
    }

    void Update()
    {
        if (!isDragging)
        {
            // Smooth snap
            Vector2 pos = contentContainer.anchoredPosition;
            pos.x = Mathf.Lerp(pos.x, targetX, Time.deltaTime * snapSpeed);
            contentContainer.anchoredPosition = pos;
        }

        UpdateCardVisuals();
    }

    // ── Drag Handlers ─────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging    = true;
        dragStartX    = eventData.position.x;
        containerStartX = contentContainer.anchoredPosition.x;
    }

    public void OnDrag(PointerEventData eventData)
    {
        float delta = eventData.position.x - dragStartX;
        contentContainer.anchoredPosition = new Vector2(containerStartX + delta,
            contentContainer.anchoredPosition.y);

        // Update current index based on container position
        float rawIndex = -contentContainer.anchoredPosition.x / cardSpacing;
        currentIndex = Mathf.RoundToInt(Mathf.Clamp(rawIndex, 0, cards.Count - 1));
        UpdateSelectionUI();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;

        // Snap to nearest card
        float rawIndex = -contentContainer.anchoredPosition.x / cardSpacing;
        currentIndex   = Mathf.RoundToInt(Mathf.Clamp(rawIndex, 0, cards.Count - 1));
        SnapToIndex(currentIndex);
        UpdateSelectionUI();
    }

    // ── Helpers ───────────────────────────────────────────────────

    void SnapToIndex(int index, bool instant = false)
    {
        currentIndex = Mathf.Clamp(index, 0, cards.Count - 1);
        targetX = -currentIndex * cardSpacing;

        if (instant)
            contentContainer.anchoredPosition = new Vector2(targetX, 0);
    }

    void UpdateCardVisuals()
    {
        float containerX = contentContainer.anchoredPosition.x;

        for (int i = 0; i < cards.Count; i++)
        {
            // World-space center X of this card
            float cardX = i * cardSpacing + containerX;
            // Normalized distance from screen center (0 = centered, 1 = one card away)
            float normalizedDist = Mathf.Abs(cardX) / cardSpacing;
            cards[i].ApplyVisualState(normalizedDist);
        }
    }

    void UpdateSelectionUI()
    {
        if (selectedNameText != null && currentIndex < cards.Count)
            selectedNameText.text = cards[currentIndex].pictureName;
    }

    void OnSelectClicked()
    {
        if (currentIndex >= cards.Count) return;
        LoadSelectedPicture();
    }

    /// <summary>Can also be called by clicking directly on the center card.</summary>
    public void LoadSelectedPicture()
    {
        PictureCard card = cards[currentIndex];
        GameData.selectedSprite      = card.outlineSprite;
        GameData.selectedPictureName = card.pictureName;
        GameData.totalRegions        = card.totalRegions;
        SceneManager.LoadScene(paintSceneName);
    }

    // ── Navigate via buttons (optional) ──────────────────────────
    public void GoLeft()
    {
        SnapToIndex(currentIndex - 1);
        UpdateSelectionUI();
    }

    public void GoRight()
    {
        SnapToIndex(currentIndex + 1);
        UpdateSelectionUI();
    }
}
