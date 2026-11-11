using UnityEngine;
using UnityEngine.AI;

// The gameplay and navigation still use the X/Z plane; this sprite faces the camera.
[DisallowMultipleComponent]
public sealed class PixelCharacterVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private Sprite[] expressions;
    private float reactionRemaining;
    private int reaction;
    public void ConfigureExpressions(Sprite[] sprites) => expressions = sprites;
    public Sprite TitleFrame(float elapsed)
    {
        if (expressions != null && expressions.Length == 4 && elapsed % 4 > 3) return expressions[0];
        return frames != null && frames.Length == 8 ? frames[Mathf.FloorToInt(elapsed * 6) % 2] : null;
    }
    public void React(bool winning)
    {
        reaction = winning ? 0 : 1;
        reactionRemaining = 2f;
    }

    private NavMeshAgent agent;
    private Vector3 previousPosition;
    private bool hasPreviousPosition;
    private float walkTime;
    private float gestureRemaining;
    private float gestureLength;
    private CasinoGesture gesture;
    private AIDealer dealer;
    private CasinoCustomerAI customer;
    private CasinoPlayerFloatCarrier carrier;
    private Vector3 restScale;

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

    public void PlayGesture(CasinoGesture type, float duration)
    {
        gesture = type;
        gestureLength = Mathf.Max(0.2f, duration);
        gestureRemaining = gestureLength;
    }

    private void Awake()
    {
        agent = GetComponentInParent<NavMeshAgent>();
        dealer = GetComponentInParent<AIDealer>();
        customer = GetComponentInParent<CasinoCustomerAI>();
        carrier = GetComponentInParent<CasinoPlayerFloatCarrier>();
        restScale = spriteRenderer != null ? spriteRenderer.transform.localScale : Vector3.one;
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
        reactionRemaining = Mathf.Max(0, reactionRemaining - Time.deltaTime);
        if (!walking && expressions != null && expressions.Length == 4)
        {
            int index = reactionRemaining > 0 ? reaction : dealer != null && (dealer.NeedsBreak || dealer.IsResting) ? 2 :
                gestureRemaining > 0 ? 3 : -1;
            if (index >= 0) spriteRenderer.sprite = expressions[index];
        }
        spriteRenderer.transform.rotation = camera.transform.rotation;
        bool seated = customer != null && customer.IsSeated || dealer != null && dealer.IsResting;
        bool tired = dealer != null && dealer.NeedsBreak;
        float bob = walking ? Mathf.Abs(Mathf.Sin(walkTime * 12)) * .055f : Mathf.Sin(Time.time * 2.2f) * .014f;
        spriteRenderer.transform.localScale = new Vector3(restScale.x, restScale.y * (seated ? .87f : 1), restScale.z);
        spriteRenderer.transform.rotation *= Quaternion.Euler(0, 0, tired ? -6 : walking ? Mathf.Sin(walkTime * 6) * 2 : 0);
        if (gestureRemaining > 0f)
        {
            gestureRemaining = Mathf.Max(0f, gestureRemaining - Time.deltaTime);
            float pose = Mathf.Sin((1f - gestureRemaining / gestureLength) * Mathf.PI);
            float reach = gesture == CasinoGesture.Collect ? -.10f : .12f;
            spriteRenderer.transform.localPosition = new Vector3(pose * reach, .04f + pose * .12f, 0);
            spriteRenderer.transform.rotation *= Quaternion.Euler(0, 0, pose * (gesture == CasinoGesture.Pay ? 8 : -8));
        }
        else
            spriteRenderer.transform.localPosition = new Vector3(0f, .04f + bob + (carrier != null && carrier.IsCarrying ? .025f : 0), 0f);
    }
}
