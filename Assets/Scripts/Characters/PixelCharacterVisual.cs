using UnityEngine;
using UnityEngine.AI;

// The gameplay and navigation still use the X/Z plane; this sprite faces the camera.
[DisallowMultipleComponent]
public sealed class PixelCharacterVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] frames;

    private NavMeshAgent agent;
    private Vector3 previousPosition;
    private bool hasPreviousPosition;
    private float walkTime;
    private float gestureRemaining;
    private float gestureLength;

    public void Configure(SpriteRenderer renderer, Sprite[] characterFrames)
    {
        spriteRenderer = renderer;
        frames = characterFrames;
    }

    public static int FrameIndex(Vector3 forward, bool walking, float elapsed)
    {
        int direction = Mathf.Abs(forward.x) > Mathf.Abs(forward.z)
            ? (forward.x < 0f ? 2 : 3)
            : (forward.z > 0f ? 1 : 0);
        int step = walking && Mathf.FloorToInt(elapsed * 6f) % 2 == 1 ? 1 : 0;
        return direction * 2 + step;
    }

    public void PlayGesture(float duration)
    {
        gestureLength = Mathf.Max(0.2f, duration);
        gestureRemaining = gestureLength;
    }

    private void Awake()
    {
        agent = GetComponentInParent<NavMeshAgent>();
        previousPosition = transform.position;
    }

    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (spriteRenderer == null || frames == null || frames.Length != 8 || camera == null)
        {
            return;
        }

        Transform actor = transform.parent != null ? transform.parent : transform;
        float speed = agent != null && agent.enabled && agent.isOnNavMesh
            ? agent.velocity.magnitude
            : hasPreviousPosition && Time.deltaTime > 0f
                ? Vector3.Distance(actor.position, previousPosition) / Time.deltaTime : 0f;
        previousPosition = actor.position;
        hasPreviousPosition = true;
        bool walking = speed > 0.12f;
        walkTime = walking ? walkTime + Time.deltaTime : 0f;

        spriteRenderer.sprite = frames[FrameIndex(actor.forward, walking, walkTime)];
        spriteRenderer.transform.rotation = camera.transform.rotation;
        if (gestureRemaining > 0f)
        {
            gestureRemaining = Mathf.Max(0f, gestureRemaining - Time.deltaTime);
            float pose = Mathf.Sin((1f - gestureRemaining / gestureLength) * Mathf.PI);
            spriteRenderer.transform.localPosition = new Vector3(0f, 0.04f + pose * 0.10f, 0f);
        }
        else
            spriteRenderer.transform.localPosition = new Vector3(0f, 0.04f, 0f);
    }
}
