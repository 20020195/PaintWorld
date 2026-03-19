using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// Unity-style HSV Color picker panel.
/// Shows Hue Ring, SV Square, R/G/B sliders, Hex input, and a live preview swatch.
/// </summary>
public class RGBColorPicker : MonoBehaviour
{
    [Header("HSV Pickers")]
    public RawImage hueRingImage;
    public RawImage svSquareImage;
    public RectTransform hueKnob;
    public RectTransform svKnob;

    [Header("Sliders")]
    public Slider sliderR;
    public Slider sliderG;
    public Slider sliderB;

    [Header("Labels")]
    public Text labelR;
    public Text labelG;
    public Text labelB;

    [Header("Hex")]
    public InputField hexInput;

    [Header("Preview")]
    public Image previewSwatch;

    [Header("Buttons")]
    public Button confirmButton;
    public Button cancelButton;

    private Action<Color> onConfirm;
    private Color currentColor = Color.white;
    
    private float currentHue = 0f;
    private float currentSat = 0f;
    private float currentVal = 1f;

    private Texture2D svTexture;
    private Color32[] svPixels;
    private const int SV_SIZE = 64;

    private bool isUpdatingUI = false;

    void Awake()
    {
        if (sliderR) sliderR.onValueChanged.AddListener(_ => OnSliderChanged());
        if (sliderG) sliderG.onValueChanged.AddListener(_ => OnSliderChanged());
        if (sliderB) sliderB.onValueChanged.AddListener(_ => OnSliderChanged());
        
        if (hexInput) hexInput.onEndEdit.AddListener(OnHexChanged);

        if (confirmButton) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton)  cancelButton.onClick.AddListener(OnCancel);

        // Generate Hue Ring Texture
        if (hueRingImage != null)
        {
            hueRingImage.texture = GenerateHueRing(128, 0.75f);
            var hueHandler = hueRingImage.gameObject.AddComponent<ColorPickerInteractable>();
            hueHandler.onDragPoint = OnHueRingInteract;
        }

        // Setup SV Square
        if (svSquareImage != null)
        {
            svTexture = new Texture2D(SV_SIZE, SV_SIZE, TextureFormat.RGB24, false);
            svPixels = new Color32[SV_SIZE * SV_SIZE];
            svSquareImage.texture = svTexture;

            var svHandler = svSquareImage.gameObject.AddComponent<ColorPickerInteractable>();
            svHandler.onDragPoint = OnSVSquareInteract;
        }
    }

    public void Show(Color initialColor, Action<Color> callback)
    {
        gameObject.SetActive(true);

        onConfirm = callback;
        currentColor = initialColor;
        Color.RGBToHSV(currentColor, out currentHue, out currentSat, out currentVal);
        
        UpdateUIFromColor();
    }

    void OnHueRingInteract(PointerEventData ped)
    {
        if (!hueRingImage) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(hueRingImage.rectTransform, ped.position, ped.pressEventCamera, out Vector2 localPos);
        
        float angle = Mathf.Atan2(localPos.y, localPos.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;
        currentHue = angle / 360f;

        currentColor = Color.HSVToRGB(currentHue, currentSat, currentVal);
        UpdateUIFromColor();
    }

    void OnSVSquareInteract(PointerEventData ped)
    {
        if (!svSquareImage) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(svSquareImage.rectTransform, ped.position, ped.pressEventCamera, out Vector2 localPos);
        Rect rect = svSquareImage.rectTransform.rect;
        
        float s = Mathf.Clamp01((localPos.x - rect.xMin) / rect.width);
        float v = Mathf.Clamp01((localPos.y - rect.yMin) / rect.height);
        
        currentSat = s;
        currentVal = v;
        currentColor = Color.HSVToRGB(currentHue, currentSat, currentVal);
        UpdateUIFromColor();
    }

    void OnSliderChanged()
    {
        if (isUpdatingUI) return;
        currentColor = new Color(
            sliderR ? sliderR.value : 0f,
            sliderG ? sliderG.value : 0f,
            sliderB ? sliderB.value : 0f);
        
        Color.RGBToHSV(currentColor, out currentHue, out currentSat, out currentVal);
        UpdateUIFromColor(skipSliders: true);
    }

    void OnHexChanged(string hex)
    {
        if (isUpdatingUI) return;
        if (!hex.StartsWith("#")) hex = "#" + hex;
        if (ColorUtility.TryParseHtmlString(hex, out Color parsedColor))
        {
            currentColor = parsedColor;
            Color.RGBToHSV(currentColor, out currentHue, out currentSat, out currentVal);
            UpdateUIFromColor();
        }
    }

    void UpdateUIFromColor(bool skipSliders = false)
    {
        isUpdatingUI = true;

        if (previewSwatch) previewSwatch.color = currentColor;

        if (!skipSliders)
        {
            if (sliderR) sliderR.value = currentColor.r;
            if (sliderG) sliderG.value = currentColor.g;
            if (sliderB) sliderB.value = currentColor.b;
        }

        if (labelR) labelR.text = $"R  {Mathf.RoundToInt(currentColor.r * 255)}";
        if (labelG) labelG.text = $"G  {Mathf.RoundToInt(currentColor.g * 255)}";
        if (labelB) labelB.text = $"B  {Mathf.RoundToInt(currentColor.b * 255)}";

        if (hexInput && !hexInput.isFocused)
        {
            hexInput.text = ColorUtility.ToHtmlStringRGB(currentColor);
        }

        // Update SV Square Texture
        if (svTexture != null)
        {
            for (int y = 0; y < SV_SIZE; y++)
            {
                float v = y / (float)(SV_SIZE - 1);
                for (int x = 0; x < SV_SIZE; x++)
                {
                    float s = x / (float)(SV_SIZE - 1);
                    svPixels[y * SV_SIZE + x] = Color.HSVToRGB(currentHue, s, v);
                }
            }
            svTexture.SetPixels32(svPixels);
            svTexture.Apply();
        }

        // Update Knobs
        if (hueKnob != null && hueRingImage != null)
        {
            float angle = currentHue * Mathf.PI * 2f;
            float radius = (hueRingImage.rectTransform.rect.width / 2f) * 0.875f; // middle of the ring
            hueKnob.anchoredPosition = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        if (svKnob != null && svSquareImage != null)
        {
            Rect rect = svSquareImage.rectTransform.rect;
            float px = rect.width * currentSat;
            float py = rect.height * currentVal;
            svKnob.anchoredPosition = new Vector2(rect.xMin + px, rect.yMin + py);
        }

        isUpdatingUI = false;
    }

    private Texture2D GenerateHueRing(int size, float innerRadiusPct)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];
        float center = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float radiusPct = dist / center;

                if (radiusPct >= innerRadiusPct && radiusPct <= 1f)
                {
                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (angle < 0) angle += 360f;
                    float hue = angle / 360f;
                    Color col = Color.HSVToRGB(hue, 1f, 1f);
                    
                    float threshold = 1.5f / center;
                    float aaAlpha = 1f;
                    if (1f - radiusPct < threshold) aaAlpha = (1f - radiusPct) / threshold;
                    else if (radiusPct - innerRadiusPct < threshold) aaAlpha = (radiusPct - innerRadiusPct) / threshold;
                    
                    col.a = aaAlpha;
                    pixels[y * size + x] = col;
                }
                else
                {
                    pixels[y * size + x] = new Color32(0,0,0,0);
                }
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    void OnConfirm()
    {
        gameObject.SetActive(false);
        onConfirm?.Invoke(currentColor);
    }

    void OnCancel() => gameObject.SetActive(false);
}

public class ColorPickerInteractable : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public Action<PointerEventData> onDragPoint;
    public void OnPointerDown(PointerEventData eventData) => onDragPoint?.Invoke(eventData);
    public void OnDrag(PointerEventData eventData) => onDragPoint?.Invoke(eventData);
}
