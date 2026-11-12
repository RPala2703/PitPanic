using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

// The gameplay and navigation still use the X/Z plane; this sprite faces the camera.
[DisallowMultipleComponent]
public sealed class PixelCharacterVisual : MonoBehaviour
{
    public const int AnimationFramesPerDirection = 12;
    public const int DirectionsPerPose = 4;
    public const int PoseCount = 13;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private Sprite[] expressions;
    [SerializeField] private Texture2D animationAtlas;
    [SerializeField] private Texture2D customerAnimationAtlas;
    public void ConfigureCustomerAnimation(Texture2D texture) => customerAnimationAtlas = texture;
    public bool UsesCustomerAnimation => customer != null && animationAtlas == customerAnimationAtlas;
    private static readonly Dictionary<Texture2D, Sprite[]> AnimationCache = new Dictionary<Texture2D, Sprite[]>();
    private static Sprite vipCrown;
    private static Sprite vipLapel;
    private SpriteRenderer crownRenderer;
    private SpriteRenderer lapelRenderer;
    private Color spriteColor = Color.white;
    private bool vip;
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
    public bool HasFrameAnimation => animationFrames != null &&
        animationFrames.Length == PoseCount * DirectionsPerPose * AnimationFramesPerDirection;
    public bool IsVip => vip;
    public void ConfigureAnimation(Texture2D texture) => animationAtlas = texture;
    private float reactionRemaining;
    private int reaction;
    public void ConfigureExpressions(Sprite[] sprites) => expressions = sprites;
    public void SetVip(bool value)
    {
        vip = value;
        if (spriteRenderer == null) return;
        if (value) EnsureVipAccessories();
        if (crownRenderer != null) crownRenderer.enabled = value;
        if (lapelRenderer != null) lapelRenderer.enabled = value;
        spriteRenderer.color = value ? Color.Lerp(spriteColor, new Color(1f, .9f, .62f), .45f) : spriteColor;
    }
    public Sprite TitleFrame(float elapsed)
    {
        if (animationFrames != null)
            return animationFrames[(int)CasinoAnimationPose.Idle * DirectionsPerPose * AnimationFramesPerDirection +
                Mathf.FloorToInt(elapsed * 3) % AnimationFramesPerDirection];
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
        if (customer != null && customerAnimationAtlas != null) animationAtlas = customerAnimationAtlas;
        carrier = GetComponentInParent<CasinoPlayerFloatCarrier>();
        restScale = spriteRenderer != null ? spriteRenderer.transform.localScale : Vector3.one;
        if (spriteRenderer != null) spriteColor = spriteRenderer.color;
        previousPosition = transform.position;
        if (animationAtlas != null)
        {
            if (!AnimationCache.TryGetValue(animationAtlas, out animationFrames))
            {
                animationFrames = new Sprite[PoseCount * DirectionsPerPose * AnimationFramesPerDirection];
                for (int row = 0; row < PoseCount * DirectionsPerPose; row++)
                for (int frame = 0; frame < AnimationFramesPerDirection; frame++)
                    animationFrames[row * AnimationFramesPerDirection + frame] = Sprite.Create(animationAtlas,
                        new Rect(frame * 48, row * 64, 48, 64), new Vector2(.5f, 0), 30);
                AnimationCache.Add(animationAtlas, animationFrames);
            }
        }
    }

    private void EnsureVipAccessories()
    {
        if (vipCrown == null || vipLapel == null) CreateVipSprites();
        if (crownRenderer == null)
            crownRenderer = CreateAccessory("VIP Crown", vipCrown, new Vector3(0, 1.88f, -.02f), 2);
        if (lapelRenderer == null)
            lapelRenderer = CreateAccessory("VIP Gold Lapel", vipLapel, new Vector3(.28f, .86f, -.025f), 2);
    }

    private SpriteRenderer CreateAccessory(string objectName, Sprite sprite, Vector3 localPosition, int orderOffset)
    {
        GameObject accessory = new GameObject(objectName);
        accessory.transform.SetParent(spriteRenderer.transform, false);
        accessory.transform.localPosition = localPosition;
        SpriteRenderer renderer = accessory.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerID = spriteRenderer.sortingLayerID;
        renderer.sortingOrder = spriteRenderer.sortingOrder + orderOffset;
        renderer.enabled = vip;
        return renderer;
    }

    private static void CreateVipSprites()
    {
        Color32 transparent = new Color32(0, 0, 0, 0);
        Color32 ink = new Color32(53, 26, 43, 255);
        Color32 gold = new Color32(244, 191, 70, 255);
        Color32 shine = new Color32(255, 239, 154, 255);
        Texture2D crown = new Texture2D(18, 12, TextureFormat.RGBA32, false);
        crown.filterMode = FilterMode.Point;
        Color32[] pixels = new Color32[18 * 12];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = transparent;
        for (int x = 2; x < 16; x++) { pixels[2 * 18 + x] = ink; pixels[3 * 18 + x] = gold; }
        for (int x = 3; x < 15; x++) pixels[4 * 18 + x] = gold;
        for (int x = 4; x < 14; x++) pixels[5 * 18 + x] = gold;
        for (int y = 2; y <= 8; y++)
        {
            pixels[y * 18 + 1] = gold; pixels[y * 18 + 2] = ink;
            pixels[y * 18 + 8] = gold; pixels[y * 18 + 9] = ink;
            pixels[y * 18 + 15] = gold; pixels[y * 18 + 16] = ink;
        }
        for (int x = 2; x <= 4; x++) pixels[6 * 18 + x] = gold;
        for (int x = 7; x <= 10; x++) pixels[8 * 18 + x] = gold;
        for (int x = 13; x <= 15; x++) pixels[6 * 18 + x] = gold;
        pixels[8 * 18 + 2] = shine; pixels[8 * 18 + 8] = shine; pixels[8 * 18 + 15] = shine;
        crown.SetPixels32(pixels);
        crown.Apply(false, true);
        crown.hideFlags = HideFlags.HideAndDontSave;
        vipCrown = Sprite.Create(crown, new Rect(0, 0, 18, 12), new Vector2(.5f, 0), 30);
        vipCrown.hideFlags = HideFlags.HideAndDontSave;

        Texture2D lapel = new Texture2D(10, 12, TextureFormat.RGBA32, false);
        lapel.filterMode = FilterMode.Point;
        Color32[] badge = new Color32[10 * 12];
        for (int i = 0; i < badge.Length; i++) badge[i] = transparent;
        for (int y = 1; y < 11; y++) { badge[y * 10 + 3] = ink; badge[y * 10 + 6] = ink; }
        for (int y = 2; y < 10; y++) { badge[y * 10 + 4] = gold; badge[y * 10 + 5] = gold; }
        badge[8 * 10 + 4] = shine; badge[4 * 10 + 5] = shine;
        lapel.SetPixels32(badge);
        lapel.Apply(false, true);
        lapel.hideFlags = HideFlags.HideAndDontSave;
        vipLapel = Sprite.Create(lapel, new Rect(0, 0, 10, 12), new Vector2(.5f, .5f), 30);
        vipLapel.hideFlags = HideFlags.HideAndDontSave;
    }

    private void LateUpdate()
    {
        if (Time.timeScale == 0) return;
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
        CasinoAnimationPose pose = carrier != null && carrier.IsCarrying ? CasinoAnimationPose.Carry :
            walking ? CasinoAnimationPose.Walk :
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
            int frame = Mathf.Min(AnimationFramesPerDirection - 1,
                Mathf.FloorToInt(poseElapsed / duration * AnimationFramesPerDirection));
            if (currentPose == CasinoAnimationPose.Carry && !walking) frame = 0;
            if (currentPose == CasinoAnimationPose.Sit && standing)
                frame = AnimationFramesPerDirection - 1 - frame;
            spriteRenderer.sprite = animationFrames[
                ((int)currentPose * DirectionsPerPose + direction) * AnimationFramesPerDirection + frame];
        }
        spriteRenderer.transform.rotation = camera.transform.rotation;
        spriteRenderer.transform.localScale = new Vector3(restScale.x, restScale.y * (seated ? .87f : 1), restScale.z);
        gestureRemaining = Mathf.Max(0, gestureRemaining - Time.deltaTime);
        actionRemaining = Mathf.Max(0, actionRemaining - Time.deltaTime);
        spriteRenderer.transform.localPosition = new Vector3(0, .04f, 0);
    }
}
