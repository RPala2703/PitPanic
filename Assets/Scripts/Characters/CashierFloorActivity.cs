using UnityEngine;

[DisallowMultipleComponent]
public sealed class CashierFloorActivity : MonoBehaviour
{
    [SerializeField] private CasinoCharacterVisual cashier;
    private CasinoCashDeskInteraction desk;
    private Transform chip;
    private float nextGesture;

    public void Configure(CasinoCharacterVisual visual) => cashier = visual;

    private void Awake()
    {
        desk = GetComponent<CasinoCashDeskInteraction>();
        chip = new GameObject("Cashier Working Chip").transform;
        chip.SetParent(transform, false);
        SpriteRenderer renderer = PixelWorldProps.CreateSurface(chip, "Pixel Cashier Chip",
            PixelWorldProps.Chip, new Vector2(.25f, .25f), -4);
        renderer.color = new Color(.98f, .76f, .34f);
    }

    private void Update()
    {
        bool busy = desk != null && (desk.ActiveCustomer != null || desk.ActiveOrder != null);
        chip.gameObject.SetActive(busy);
        if (!busy) return;
        float progress = Mathf.PingPong(Time.time, 1);
        chip.localPosition = Vector3.Lerp(new Vector3(-.6f, 1.3f, .4f), new Vector3(.6f, 1.3f, -.4f), progress)
            + Vector3.up * (.15f * Mathf.Sin(progress * Mathf.PI));
        if (Time.time >= nextGesture && cashier != null)
        {
            nextGesture = Time.time + 1.1f;
            cashier.PlayGesture(CasinoGesture.Pay, transform.position + Vector3.back, .8f);
            desk.ActiveCustomer?.GetComponentInChildren<CasinoCharacterVisual>()?.PlayGesture(
                CasinoGesture.Collect, transform.position, .8f);
        }
    }
}
