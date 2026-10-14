using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class CasinoChipDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private CashDeskPackMinigame minigame;
    private RectTransform rectTransform;
    private RectTransform canvasRect;
    private int denominationIndex;
    private Vector2 homePosition;
    private int homeSiblingIndex;
    private bool dragging;

    public void Configure(
        CashDeskPackMinigame owner,
        RectTransform rootCanvas,
        int denomination,
        Vector2 originalPosition)
    {
        minigame = owner;
        canvasRect = rootCanvas;
        denominationIndex = denomination;
        homePosition = originalPosition;
        rectTransform = transform as RectTransform;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = minigame != null && minigame.CanDragRackChip;
        if (!dragging || rectTransform == null)
        {
            return;
        }

        homeSiblingIndex = rectTransform.GetSiblingIndex();
        rectTransform.SetAsLastSibling();
        minigame.SetChipDragFeedback(denominationIndex, true);
        MoveToPointer(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragging)
        {
            MoveToPointer(eventData.position);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging)
        {
            return;
        }

        dragging = false;
        minigame.TryDropRackChip(denominationIndex, eventData.position);
        RestorePosition();
        minigame.SetChipDragFeedback(denominationIndex, false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (minigame != null && minigame.IsCashOutMode)
        {
            minigame.SelectCashOutChip(denominationIndex);
        }
    }

    private void MoveToPointer(Vector2 screenPoint)
    {
        if (canvasRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPoint, null, out Vector2 localPoint))
        {
            rectTransform.anchoredPosition = localPoint;
        }
    }

    private void RestorePosition()
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchoredPosition = homePosition;
        rectTransform.SetSiblingIndex(Mathf.Clamp(homeSiblingIndex, 0, rectTransform.parent.childCount - 1));
    }
}
