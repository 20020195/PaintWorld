using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates and manages the color palette UI.
/// Includes preset color swatches + a "+" custom color button that opens the RGB picker.
/// Swatches are circular (Mask + Knob sprite). Selected state shown via a white dot child.
/// </summary>
public class ColorPaletteUI : MonoBehaviour
{
    [Header("References")]
    public PaintController paintController;
    public RGBColorPicker rgbColorPicker;

    [Header("Palette Colors")]
    public List<Color> colors = new List<Color>
    {
        new Color(0.27f, 0.73f, 1.00f),  // Sky Blue
        new Color(1.00f, 0.90f, 0.20f),  // Sun Yellow
        new Color(0.56f, 0.46f, 0.80f),  // Mountain Purple
        new Color(0.25f, 0.70f, 0.25f),  // Tree Green
        new Color(0.90f, 0.25f, 0.25f),  // House Red
        new Color(0.60f, 0.40f, 0.15f),  // Ground Brown
        new Color(1.00f, 0.60f, 0.20f),  // Orange
        new Color(0.90f, 0.50f, 0.70f),  // Pink
        new Color(0.30f, 0.80f, 0.70f),  // Teal
        new Color(0.95f, 0.95f, 0.95f),  // White/Eraser
    };

    [Header("Style")]
    public float buttonSize = 40f;
    public Sprite circleSprite; // Assign via Inspector or PaintSceneSetup

    private List<Button> colorButtons = new List<Button>();
    private int selectedIndex = -1;
    private Color customColor = Color.white;
    private Button customColorButton;
    private Image customColorInner;

    // Task 1.1 — new fields for SelectedDot tracking
    private List<GameObject> selectedDots = new List<GameObject>();
    private GameObject customSelectedDot;

    void Start()
    {
        CreateColorButtons();

        // Select first color by default
        if (colorButtons.Count > 0)
            SelectColor(0);
    }

    // Task 1.3 — updated CreateColorButtons
    void CreateColorButtons()
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
        colorButtons.Clear();
        selectedDots.Clear();

        // ── Preset color buttons ──────────────────────────────────
        for (int i = 0; i < colors.Count; i++)
        {
            int idx = i;
            Color col = colors[i];

            GameObject btnGO = CreateSwatchGO($"Color_{i}", col);
            btnGO.transform.SetParent(transform, false);

            Button btn = btnGO.GetComponent<Button>();
            btn.onClick.AddListener(() => SelectColor(idx));
            colorButtons.Add(btn);

            // Track SelectedDot for each preset swatch
            Transform dot = btnGO.transform.Find("SelectedDot");
            selectedDots.Add(dot != null ? dot.gameObject : null);
        }

        // ── Custom color "+" button ───────────────────────────────
        GameObject customGO = CreateSwatchGO("CustomColor", customColor);
        customGO.transform.SetParent(transform, false);

        // Add "+" label
        GameObject plusGO = new GameObject("Plus");
        plusGO.transform.SetParent(customGO.transform, false);
        RectTransform plusRT = plusGO.AddComponent<RectTransform>();
        plusRT.anchorMin = Vector2.zero;
        plusRT.anchorMax = Vector2.one;
        plusRT.offsetMin = Vector2.zero;
        plusRT.offsetMax = Vector2.zero;
        Text plusText = plusGO.AddComponent<Text>();
        plusText.text = "+";
        plusText.alignment = TextAnchor.MiddleCenter;
        plusText.fontSize = 28;
        plusText.fontStyle = FontStyle.Bold;
        plusText.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        plusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        customColorButton = customGO.GetComponent<Button>();

        // customColorInner is the root Image of the custom GO (Task 1.3)
        customColorInner = customGO.GetComponent<Image>();

        // Track SelectedDot for custom swatch (Task 1.3)
        Transform customDot = customGO.transform.Find("SelectedDot");
        customSelectedDot = customDot != null ? customDot.gameObject : null;

        customColorButton.onClick.AddListener(OnCustomColorClicked);
    }

    // Task 1.2 — rewritten CreateSwatchGO: circular swatch with SelectedDot child
    GameObject CreateSwatchGO(string name, Color color)
    {
        Sprite knob = circleSprite != null
            ? circleSprite
            : Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");

        // Root GO: RectTransform + Image (swatch color, circle) + Mask + Button
        GameObject go = new GameObject(name);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(buttonSize, buttonSize);

        Image img = go.AddComponent<Image>();
        img.color = color;
        img.sprite = knob;
        img.type = Image.Type.Simple;

        Mask mask = go.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minWidth        = buttonSize;
        le.minHeight       = buttonSize;
        le.preferredWidth  = buttonSize;
        le.preferredHeight = buttonSize;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        // Child "SelectedDot": white circle, 14×14, hidden by default
        GameObject dot = new GameObject("SelectedDot");
        dot.transform.SetParent(go.transform, false);
        RectTransform dotRT = dot.AddComponent<RectTransform>();
        dotRT.sizeDelta = new Vector2(14f, 14f);
        dotRT.anchorMin = new Vector2(0.5f, 0.5f);
        dotRT.anchorMax = new Vector2(0.5f, 0.5f);
        dotRT.anchoredPosition = Vector2.zero;
        Image dotImg = dot.AddComponent<Image>();
        dotImg.color = Color.white;
        dotImg.sprite = knob;
        dotImg.type = Image.Type.Simple;
        dot.SetActive(false);

        return go;
    }

    // Task 1.4 — rewritten SelectColor: uses SelectedDot, no yellow border
    void SelectColor(int index)
    {
        // Hide old selected dot
        if (selectedIndex >= 0 && selectedIndex < selectedDots.Count && selectedDots[selectedIndex] != null)
            selectedDots[selectedIndex].SetActive(false);

        // Hide custom dot
        if (customSelectedDot != null)
            customSelectedDot.SetActive(false);

        selectedIndex = index;

        // Show new selected dot
        if (index >= 0 && index < selectedDots.Count && selectedDots[index] != null)
            selectedDots[index].SetActive(true);

        if (paintController != null)
            paintController.SetSelectedColor(colors[index]);
    }

    // Task 1.5 — rewritten SelectCustomColor: uses SelectedDot, no yellow border
    void SelectCustomColor(Color color)
    {
        // Hide preset dot
        if (selectedIndex >= 0 && selectedIndex < selectedDots.Count && selectedDots[selectedIndex] != null)
            selectedDots[selectedIndex].SetActive(false);
        selectedIndex = -1;

        // Show custom dot
        if (customSelectedDot != null)
            customSelectedDot.SetActive(true);

        // Update custom swatch color
        if (customColorInner != null)
            customColorInner.color = color;

        if (paintController != null)
            paintController.SetSelectedColor(color);
    }

    void OnCustomColorClicked()
    {
        if (rgbColorPicker == null)
        {
            Debug.LogWarning("[PaintGame] RGBColorPicker reference not set on ColorPaletteUI!");
            return;
        }

        rgbColorPicker.Show(customColor, (chosenColor) =>
        {
            customColor = chosenColor;
            SelectCustomColor(chosenColor);
        });
    }
}
