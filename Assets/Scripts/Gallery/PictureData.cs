using UnityEngine;

/// <summary>
/// Data container for a picture level.
/// Create instances of this via Right Click -> Create -> Paint Game -> Picture Data.
/// </summary>
[CreateAssetMenu(fileName = "NewPictureData", menuName = "Paint Game/Picture Data")]
public class PictureData : ScriptableObject
{
    public string pictureName;
    public Sprite outlineSprite;
}
