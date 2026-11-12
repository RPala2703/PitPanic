using UnityEngine;

[DisallowMultipleComponent]
public sealed class CasinoSeatVisual : MonoBehaviour
{
    [SerializeField] private Transform chair;
    private CasinoSeat seat;
    private Vector3 home;
    private Vector3 pullOut;
    public Transform Chair => chair;

    public void Configure(Transform visual) => chair = visual;

    private void Start()
    {
        seat = GetComponent<CasinoSeat>();
        home = chair.localPosition;
        pullOut = transform.InverseTransformDirection(seat.Facing * Vector3.back) * .15f;
    }

    private void Update()
    {
        bool sitting = seat.Occupant != null && seat.Occupant.IsSeated;
        chair.localPosition = Vector3.MoveTowards(chair.localPosition,
            home + (sitting ? Vector3.zero : pullOut), Time.deltaTime * .6f);
    }
}
