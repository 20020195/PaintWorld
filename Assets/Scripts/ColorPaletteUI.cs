using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates and manages the color palette UI at the bottom of the screen.
/// Includes preset color swatches + a "+" custom color button that opens the RGB picker.
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
    public float buttonSize = 56f;
    public float borderThickness = 4f;

    private List<Button> colorButtons = new List<Button>();
    private int selectedIndex = -1;
    private Color customColor = Color.white;
    private Button customColorButton;
    private Image customColorInner;

    void Start()
    {
        CreateColorButtons();

        // Select first color by default
        if (colorButtons.Count > 0)
            SelectColor(0);
    }

    void CreateColorButtons()
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
        colorButtons.Clear();

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
        customColorInner  = customGO.transform.Find("Inner")?.GetComponent<Image>();
        customColorButton.onClick.AddListener(OnCustomColorClicked);
    }

    /// <summary>Create a bordered swatch button (border + inner color square).</summary>
    GameObject CreateSwatchGO(string name, Color color)
    {
        GameObject go = new GameObject(name);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(buttonSize, buttonSize);
        Image border = go.AddComponent<Image>();
        border.color = Color.white;
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = border;

        // Inner color
        GameObject inner = new GameObject("Inner");
        inner.transform.SetParent(go.transform, false);
        RectTransform innerRT = inner.AddComponent<RectTransform>();
        float inset = borderThickness;
        innerRT.anchorMin = Vector2.zero;
        innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = new Vector2(inset, inset);
        innerRT.offsetMax = new Vector2(-inset, -inset);
        Image innerImg = inner.AddComponent<Image>();
        innerImg.color = color;

        return go;
    }

    void SelectColor(int index)
    {
        // Deselect old
        if (selectedIndex >= 0 && selectedIndex < colorButtons.Count)
            colorButtons[selectedIndex].GetComponent<Image>().color = Color.white;

        // Deselect custom button border if needed
        if (customColorButton != null)
            customColorButton.GetComponent<Image>().color = Color.white;

        selectedIndex = index;
        colorButtons[index].GetComponent<Image>().color = new Color(1f, 0.85f, 0.1f); // yellow border

        if (paintController != null)
            paintController.SetSelectedColor(colors[index]);
    }

    void SelectCustomColor(Color color)
    {
        // Deselect all preset buttons
        if (selectedIndex >= 0 && selectedIndex < colorButtons.Count)
            colorButtons[selectedIndex].GetComponent<Image>().color = Color.white;
        selectedIndex = -1;

        // Highlight custom button
        if (customColorButton != null)
            customColorButton.GetComponent<Image>().color = new Color(1f, 0.85f, 0.1f);

        // Update inner swatch to show chosen color
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
