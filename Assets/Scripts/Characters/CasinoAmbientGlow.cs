using UnityEngine;

[DisallowMultipleComponent]
public sealed class CasinoAmbientGlow : MonoBehaviour
{
    private SpriteRenderer sprite;
    private float phase;

    private void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        phase = transform.position.x * 1.7f;
    }

    private void Update()
    {
        if (sprite != null)
        {
            float glow = .93f + .07f * Mathf.Sin(Time.time * 3 + phase);
            sprite.color = new Color(1, glow, .85f + glow * .15f);
        }
    }
}
