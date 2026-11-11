using UnityEngine;

[DisallowMultipleComponent]
public sealed class CasinoSeatVisual : MonoBehaviour
{
    [SerializeField] private Transform chair;
    private CasinoSeat seat;
    private Vector3 home;

    public void Configure(Transform visual) => chair = visual;

    private void Start()
    {
        seat = GetComponent<CasinoSeat>();
        home = chair.localPosition;
    }

    private void Update()
    {
        bool sitting = seat.Occupant != null && seat.Occupant.IsSeated;
        chair.localPosition = Vector3.MoveTowards(chair.localPosition,
            home + (sitting ? Vector3.zero : Vector3.back * .15f), Time.deltaTime * .6f);
    }
}
