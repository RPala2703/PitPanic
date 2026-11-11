using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

// The gameplay and navigation still use the X/Z plane; this sprite faces the camera.
[DisallowMultipleComponent]
public sealed class PixelCharacterVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private Sprite[] expressions;
    [SerializeField] private Texture2D animationAtlas;
    private static readonly Dictionary<Texture2D, Sprite[]> AnimationCache = new Dictionary<Texture2D, Sprite[]>();
    private Sprite[] animationFrames;
    private CasinoAnimationPose currentPose;
    private float poseElapsed;
    private bool wasSeated;
    private int gestureDirection;
    private float nextFootstep;
    private float actionRemaining;
    private float actionDuration;
    private CasinoAnimationPose requestedPose;
    private bool standing;
    public CasinoAnimationPose CurrentPose => currentPose;
    public bool HasFrameAnimation => animationFrames != null && animationFrames.Length == 416;
    public void ConfigureAnimation(Texture2D texture) => animationAtlas = texture;
    private float reactionRemaining;
    private int reaction;
    public void ConfigureExpressions(Sprite[] sprites) => expressions = sprites;
    public Sprite TitleFrame(float elapsed)
    {
        if (animationFrames != null) return animationFrames[(int)CasinoAnimationPose.Idle * 32 + Mathf.FloorToInt(elapsed * 3) % 8];
        if (expressions != null && expressions.Length == 4 && elapsed % 4 > 3) return expressions[0];
        return frames != null && frames.Length == 8 ? frames[Mathf.FloorToInt(elapsed * 6) % 2] : null;
    }
    public void React(bool winning)
    {
        reaction = winning ? 0 : 1;
        reactionRemaining = 2f;
        CasinoSoundscape.Play(winning ? CasinoSoundCue.Celebrate : CasinoSoundCue.Disappointed, transform.position);
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
        poseElapsed = 0;
    }

    public void PlayAction(CasinoAnimationPose pose, Vector3 target, float duration)
    {
        requestedPose = pose;
        actionDuration = Mathf.Max(.2f, duration);
        actionRemaining = actionDuration;
        FaceGesture(target);
        poseElapsed = 0;
    }

    public void FaceGesture(Vector3 target)
    {
        Vector3 heading = target - transform.position;
        gestureDirection = Direction(heading);
    }

    private static int Direction(Vector3 heading) => Mathf.Abs(heading.x) > Mathf.Abs(heading.z)
        ? heading.x < 0 ? 2 : 3 : heading.z > 0 ? 1 : 0;

    private void Awake()
    {
        agent = GetComponentInParent<NavMeshAgent>();
        dealer = GetComponentInParent<AIDealer>();
        customer = GetComponentInParent<CasinoCustomerAI>();
        carrier = GetComponentInParent<CasinoPlayerFloatCarrier>();
        restScale = spriteRenderer != null ? spriteRenderer.transform.localScale : Vector3.one;
        previousPosition = transform.position;
        if (animationAtlas != null)
        {
            if (!AnimationCache.TryGetValue(animationAtlas, out animationFrames))
            {
                animationFrames = new Sprite[13 * 4 * 8];
                for (int row = 0; row < 13 * 4; row++)
                for (int frame = 0; frame < 8; frame++)
                    animationFrames[row * 8 + frame] = Sprite.Create(animationAtlas,
                        new Rect(frame * 48, row * 64, 48, 64), new Vector2(.5f, 0), 30);
                AnimationCache.Add(animationAtlas, animationFrames);
            }
        }
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
        if (dealer != null)
            transform.localPosition = Vector3.MoveTowards(transform.localPosition,
                dealer.IsAtStation ? Vector3.forward * .65f : Vector3.zero, Time.deltaTime * 3f);
        walkTime = walking ? walkTime + Time.deltaTime : 0f;
        bool seated = customer != null && customer.IsSeated || dealer != null && dealer.IsResting;
        CasinoAnimationPose pose = walking ? carrier != null && carrier.IsCarrying ? CasinoAnimationPose.Carry : CasinoAnimationPose.Walk :
            reactionRemaining > 0 ? reaction == 0 ? CasinoAnimationPose.Celebrate : CasinoAnimationPose.Disappointed :
            gestureRemaining > 0 ? gesture == CasinoGesture.Deal ? CasinoAnimationPose.Deal :
                gesture == CasinoGesture.Pay ? CasinoAnimationPose.Pay : CasinoAnimationPose.Collect :
            dealer != null && dealer.IsResting ? CasinoAnimationPose.Rest :
            dealer != null && dealer.NeedsBreak ? CasinoAnimationPose.Tired :
            seated ? CasinoAnimationPose.Examine : CasinoAnimationPose.Idle;
        if (seated != wasSeated)
        {
            standing = !seated;
            currentPose = CasinoAnimationPose.Sit;
            poseElapsed = 0;
        }
        wasSeated = seated;
        if (currentPose == CasinoAnimationPose.Sit && poseElapsed < .8f && !walking) pose = CasinoAnimationPose.Sit;
        if (actionRemaining > 0 && !walking) pose = requestedPose;
        if (pose != currentPose) { currentPose = pose; poseElapsed = 0; }
        poseElapsed += Time.deltaTime;
        if (walking && Time.time >= nextFootstep)
        {
            nextFootstep = Time.time + .36f;
            CasinoSoundscape.Play(CasinoSoundCue.Step, actor.position);
        }

        spriteRenderer.sprite = frames[FrameIndex(actor.forward, walking, walkTime)];
        reactionRemaining = Mathf.Max(0, reactionRemaining - Time.deltaTime);
        if (!walking && expressions != null && expressions.Length == 4)
        {
            int index = reactionRemaining > 0 ? reaction : dealer != null && (dealer.NeedsBreak || dealer.IsResting) ? 2 :
                gestureRemaining > 0 ? 3 : -1;
            if (index >= 0) spriteRenderer.sprite = expressions[index];
        }
        if (animationFrames != null)
        {
            int direction = (gestureRemaining > 0 || actionRemaining > 0) && !walking ? gestureDirection : Direction(actor.forward);
            float duration = actionRemaining > 0 ? actionDuration : gestureRemaining > 0 ? gestureLength : currentPose == CasinoAnimationPose.Sit ? .8f :
                walking ? .65f : currentPose == CasinoAnimationPose.Idle ? 3.2f : 1.2f;
            int frame = Mathf.FloorToInt(poseElapsed / duration * 8) % 8;
            if (currentPose == CasinoAnimationPose.Sit) frame = Mathf.Min(7, Mathf.FloorToInt(poseElapsed / duration * 8));
            if (currentPose == CasinoAnimationPose.Sit && standing) frame = 7 - frame;
            spriteRenderer.sprite = animationFrames[((int)currentPose * 4 + direction) * 8 + frame];
        }
        spriteRenderer.transform.rotation = camera.transform.rotation;
        spriteRenderer.transform.localScale = new Vector3(restScale.x, restScale.y * (seated ? .87f : 1), restScale.z);
        gestureRemaining = Mathf.Max(0, gestureRemaining - Time.deltaTime);
        actionRemaining = Mathf.Max(0, actionRemaining - Time.deltaTime);
        spriteRenderer.transform.localPosition = new Vector3(0, .04f, 0);
    }
}
