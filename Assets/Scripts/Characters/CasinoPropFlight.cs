using UnityEngine;

public sealed class CasinoPropFlight : MonoBehaviour
{
    private Vector3 origin;
    private Vector3 destination;
    private float elapsed;
    private float duration;
    private CasinoSoundCue cue;
    public void Configure(Vector3 from, Vector3 to, float seconds, CasinoSoundCue sound)
    {
        origin = from; destination = to; duration = Mathf.Max(.1f, seconds); cue = sound;
        transform.localPosition = origin;
    }
    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        transform.localPosition = Vector3.Lerp(origin, destination, t * t * (3 - 2 * t))
            + Vector3.up * (.18f * Mathf.Sin(t * Mathf.PI));
        if (t < 1) return;
        CasinoSoundscape.Play(cue, transform.position);
        Destroy(this);
    }
}
