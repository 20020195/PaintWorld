using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Data holder for one picture card in the carousel.
/// </summary>
public class PictureCard : MonoBehaviour
{
    [Header("Picture Data")]
    public Sprite outlineSprite;
    public string pictureName = "Untitled";

    [Header("Visual")]
    public Image cardImage;
    public Text  nameLabel;

    void Awake()
    {
        if (nameLabel != null)
            nameLabel.text = pictureName;
    }

    /// <summary>Apply visual state based on how far this card is from the carousel center.</summary>
    public void ApplyVisualState(float normalizedDistance)
    {
        // normalizedDistance: 0 = center (selected), 1 = one card away, etc.
        float t = Mathf.Clamp01(normalizedDistance);

        float targetScale = Mathf.Lerp(1.0f, 0.70f, t);
        float targetAlpha = Mathf.Lerp(1.0f, 0.40f, t);

        transform.localScale = Vector3.Lerp(transform.localScale,
            new Vector3(targetScale, targetScale, 1f), Time.deltaTime * 12f);

        if (cardImage != null)
        {
            Color c = cardImage.color;
            c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * 12f);
            cardImage.color = c;
        }

        if (nameLabel != null)
        {
            Color c = nameLabel.color;
            c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * 12f);
            nameLabel.color = c;
        }
    }
}
