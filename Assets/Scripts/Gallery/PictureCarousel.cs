using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

/// <summary>
/// Horizontal carousel with snap-to-center behavior.
/// Spawns PictureCards dynamically at runtime based on `pictureDataList`.
/// </summary>
public class PictureCarousel : MonoBehaviour, IDragHandler, IEndDragHandler, IBeginDragHandler
{
    [Header("References")]
    public RectTransform contentContainer;   // parent of all cards
    public Button selectButton;              // "Chọn tranh này" button
    public Button backButton;                // "← Menu" button
    public Text selectedNameText;            // shows name of center card
    public Button leftArrowButton;           // ← arrow
    public Button rightArrowButton;          // → arrow

    [Header("Data")]
    public PictureData[] pictureDataList;    // Array of ScriptableObjects to load
    public GameObject cardPrefab;            // Optional prefab. If null, generated via code.

    [Header("Layout")]
    public float cardSpacing = 520f;         // distance between card centers
    public float snapSpeed   = 10f;          // lerp speed for snap

    [Header("Select")]
    public string paintSceneName = "PaintScene";

    [HideInInspector]
    public List<PictureCard> cards = new List<PictureCard>();

    // ── internal state ────────────────────────────────────────────
    private int   currentIndex = 0;
    private float targetX      = 0f;
    private bool  isDragging   = false;
    private float dragStartX   = 0f;
    private float containerStartX = 0f;

    void Start()
    {
        // 1. Spawn Cards Dynamically
        if (pictureDataList != null && pictureDataList.Length > 0)
        {
            cards.Clear();
            for (int i = 0; i < pictureDataList.Length; i++)
            {
                PictureData data = pictureDataList[i];
                if (data == null) continue;

                PictureCard card = SpawnCard(data, i);
                cards.Add(card);
            }
        }

        if (cards.Count == 0) return;

        // 2. Position cards
        for (int i = 0; i < cards.Count; i++)
        {
            RectTransform rt = cards[i].GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(i * cardSpacing, 0);
        }

        // Start at center (index 0)
        SnapToIndex(0, instant: true);

        if (selectButton != null)
            selectButton.onClick.AddListener(OnSelectClicked);

        if (backButton != null)
            backButton.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));

        if (leftArrowButton != null)
            leftArrowButton.onClick.AddListener(GoLeft);

        if (rightArrowButton != null)
            rightArrowButton.onClick.AddListener(GoRight);

        UpdateCardVisuals();
        UpdateSelectionUI();
    }

    private PictureCard SpawnCard(PictureData data, int index)
    {
        Sprite displaySprite = data.outlineSprite;
        if (SaveSystem.HasSave(data.pictureName))
        {
            Texture2D savedTex = new Texture2D(2, 2);
            if (SaveSystem.LoadPaintPreview(data.pictureName, savedTex))
            {
                displaySprite = Sprite.Create(savedTex, new Rect(0, 0, savedTex.width, savedTex.height), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        if (cardPrefab != null)
        {
            GameObject go = Instantiate(cardPrefab, contentContainer);
            go.name = $"Card_{index}";
            PictureCard card = go.GetComponent<PictureCard>();
            if (card != null)
            {
                card.outlineSprite = data.outlineSprite;
                card.pictureName   = data.pictureName;
                if (card.cardImage != null) card.cardImage.sprite = displaySprite;
                if (card.nameLabel != null) card.nameLabel.GetComponent<Text>().text = data.pictureName;
            }
            return card;
        }

        // Code Generation Fallback (if no prefab assigned)
        var cardGO = new GameObject($"Card_{index}");
        cardGO.transform.SetParent(contentContainer, false);
        var rt = cardGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(420f, 380f);
        rt.anchoredPosition = new Vector2(index * cardSpacing, 0);
        cardGO.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.28f);

        var borderGO = new GameObject("Border");
        borderGO.transform.SetParent(cardGO.transform, false);
        var brt = borderGO.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(-6, -6); brt.offsetMax = Vector2.zero;
        borderGO.AddComponent<Image>().color = new Color(0.55f, 0.35f, 0.85f, 0.5f);
        borderGO.transform.SetAsFirstSibling();

        var imgGO = new GameObject("PictureImage");
        imgGO.transform.SetParent(cardGO.transform, false);
        var irt = imgGO.AddComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.05f, 0.20f); irt.anchorMax = new Vector2(0.95f, 0.95f);
        irt.offsetMin = irt.offsetMax = Vector2.zero;
        var picImg = imgGO.AddComponent<Image>();
        picImg.sprite = displaySprite;
        picImg.preserveAspect = true;

        var nameGO = new GameObject("NameLabel");
        nameGO.transform.SetParent(cardGO.transform, false);
        var nrt = nameGO.AddComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0, 0.02f); nrt.anchorMax = new Vector2(1, 0.20f);
        nrt.offsetMin = nrt.offsetMax = Vector2.zero;
        var txt = nameGO.AddComponent<Text>();
        txt.text = data.pictureName;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 22;
        txt.color = new Color(0.9f, 0.9f, 1f);
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var cardComp = cardGO.AddComponent<PictureCard>();
        cardComp.outlineSprite = data.outlineSprite;
        cardComp.pictureName   = data.pictureName;
        cardComp.cardImage     = picImg;
        cardComp.nameLabel     = txt;

        return cardComp;
    }

    void Update()
    {
        if (!isDragging)
        {
            Vector2 pos = contentContainer.anchoredPosition;
            pos.x = Mathf.Lerp(pos.x, targetX, Time.deltaTime * snapSpeed);
            contentContainer.anchoredPosition = pos;
        }

        UpdateCardVisuals();
    }

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

        if (cards.Count > 0)
        {
            float rawIndex = -contentContainer.anchoredPosition.x / cardSpacing;
            currentIndex = Mathf.RoundToInt(Mathf.Clamp(rawIndex, 0, cards.Count - 1));
            UpdateSelectionUI();
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        if (cards.Count > 0)
        {
            float rawIndex = -contentContainer.anchoredPosition.x / cardSpacing;
            currentIndex   = Mathf.RoundToInt(Mathf.Clamp(rawIndex, 0, cards.Count - 1));
            SnapToIndex(currentIndex);
            UpdateSelectionUI();
        }
    }

    void SnapToIndex(int index, bool instant = false)
    {
        if (cards.Count == 0) return;
        currentIndex = Mathf.Clamp(index, 0, cards.Count - 1);
        targetX = -currentIndex * cardSpacing;

        if (instant)
            contentContainer.anchoredPosition = new Vector2(targetX, 0);
    }

    void UpdateCardVisuals()
    {
        if (cards.Count == 0) return;
        float containerX = contentContainer.anchoredPosition.x;

        for (int i = 0; i < cards.Count; i++)
        {
            float cardX = i * cardSpacing + containerX;
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

    public void LoadSelectedPicture()
    {
        if (currentIndex >= cards.Count) return;
        PictureCard card = cards[currentIndex];
        GameData.selectedSprite      = card.outlineSprite;
        GameData.selectedPictureName = card.pictureName;
        SceneManager.LoadScene(paintSceneName);
    }

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
