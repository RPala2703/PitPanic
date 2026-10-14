using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class CasinoPackedChipRemoveHandle : MonoBehaviour, IPointerClickHandler
{
    private CashDeskPackMinigame minigame;
    private int denominationIndex;

    public void Configure(CashDeskPackMinigame owner, int denomination)
    {
        minigame = owner;
        denominationIndex = denomination;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        minigame?.RemovePackedChip(denominationIndex);
    }
}
