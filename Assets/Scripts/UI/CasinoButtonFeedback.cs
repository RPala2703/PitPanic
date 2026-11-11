using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CasinoButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private Vector3 rest;
    private Button button;
    private void Awake() { rest = transform.localScale; button = GetComponent<Button>(); }
    public void OnPointerDown(PointerEventData data)
    {
        if (button == null || !button.IsInteractable()) return;
        transform.localScale = rest * .96f;
        CasinoSoundscape.Play(CasinoSoundCue.Button, Vector3.zero, CasinoSoundscape.Focus);
    }
    public void OnPointerUp(PointerEventData data) => transform.localScale = rest;
    public void OnPointerExit(PointerEventData data) => transform.localScale = rest;
    private void OnDisable() => transform.localScale = rest;
}
