using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Forwards drag events from the Carousel Viewport to the PictureCarousel.
/// Needed because RectMask2D sits on the viewport panel and would otherwise
/// swallow pointer events before they reach the carousel script.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CarouselDragProxy : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public PictureCarousel carousel;

    public void OnBeginDrag(PointerEventData e) => carousel?.OnBeginDrag(e);
    public void OnDrag(PointerEventData e)      => carousel?.OnDrag(e);
    public void OnEndDrag(PointerEventData e)   => carousel?.OnEndDrag(e);
}
