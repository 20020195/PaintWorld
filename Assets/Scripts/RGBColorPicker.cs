using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// RGB Color picker panel.
/// Shows R/G/B sliders with a live preview swatch.
/// Call Show(onColorPicked) to open it, it invokes the callback when confirmed.
/// </summary>
public class RGBColorPicker : MonoBehaviour
{
    [Header("Sliders")]
    public Slider sliderR;
    public Slider sliderG;
    public Slider sliderB;

    [Header("Labels")]
    public Text labelR;
    public Text labelG;
    public Text labelB;

    [Header("Preview")]
    public Image previewSwatch;

    [Header("Buttons")]
    public Button confirmButton;
    public Button cancelButton;

    private Action<Color> onConfirm;
    private Color currentColor = Color.white;

    void Awake()
    {
        // Wire sliders
        if (sliderR) sliderR.onValueChanged.AddListener(_ => OnSliderChanged());
        if (sliderG) sliderG.onValueChanged.AddListener(_ => OnSliderChanged());
        if (sliderB) sliderB.onValueChanged.AddListener(_ => OnSliderChanged());

        if (confirmButton) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton)  cancelButton.onClick.AddListener(OnCancel);
        // NOTE: initial hidden state is set by the scene setup script (SetActive(false))
        // Do NOT call SetActive(false) here — it would close the panel on the first Show() call
        // because Unity calls Awake() the first time SetActive(true) is invoked.
    }

    /// <summary>Open the picker, pre-filled with initialColor. callback is invoked with chosen color.</summary>
    public void Show(Color initialColor, Action<Color> callback)
    {
        onConfirm = callback;
        currentColor = initialColor;

        if (sliderR) sliderR.value = initialColor.r;
        if (sliderG) sliderG.value = initialColor.g;
        if (sliderB) sliderB.value = initialColor.b;

        UpdatePreview();
        gameObject.SetActive(true);
    }

    void OnSliderChanged()
    {
        currentColor = new Color(
            sliderR ? sliderR.value : 0f,
            sliderG ? sliderG.value : 0f,
            sliderB ? sliderB.value : 0f);
        UpdatePreview();
    }

    void UpdatePreview()
    {
        if (previewSwatch) previewSwatch.color = currentColor;

        if (labelR) labelR.text = $"R  {Mathf.RoundToInt(currentColor.r * 255)}";
        if (labelG) labelG.text = $"G  {Mathf.RoundToInt(currentColor.g * 255)}";
        if (labelB) labelB.text = $"B  {Mathf.RoundToInt(currentColor.b * 255)}";
    }

    void OnConfirm()
    {
        gameObject.SetActive(false);
        onConfirm?.Invoke(currentColor);
    }

    void OnCancel() => gameObject.SetActive(false);
}
