using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

// The gameplay and navigation still use the X/Z plane; this sprite faces the camera.
[DisallowMultipleComponent]
public sealed class PixelCharacterVisual : MonoBehaviour
{
    public const int AnimationFramesPerDirection = 60;
    public const int DirectionsPerPose = 4;
    public const int PoseCount = 16;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private Sprite[] expressions;
    [SerializeField] private Texture2D animationAtlas;
    [SerializeField] private Texture2D customerAnimationAtlas;
    [SerializeField] private Texture2D vipAnimationAtlas;
    [SerializeField] private Sprite approvedStanding;
    [SerializeField] private Sprite approvedVipStanding;
    [SerializeField] private Sprite playerFront;
    [SerializeField] private Sprite playerRight;
    [SerializeField] private Sprite playerBack;
    [SerializeField] private Sprite playerLeft;
    [SerializeField] private Sprite[] playerWalkFrames;
    [SerializeField] private Sprite[] dealerWalkFrames;
    private const int PlayerWalkFramesPerDirection = 4;
    private const float PlayerWalkFrameDuration = 0.18f;
    private enum PlayerFacing { Front, Right, Back, Left }
    private PlayerFacing playerFacing = PlayerFacing.Front;
    private CasinoPlayerController playerController;
    private int normalSortingOrder;
    private bool dealerWalkArt;
    private bool dealerWasWalking;
    private bool isPlayerVisual => playerController != null;
    public bool UsesApprovedStandingArt => approvedStanding != null;
    public bool UsesPlayerDirectionalArt => isPlayerVisual &&
        playerFront != null && playerRight != null && playerBack != null && playerLeft != null;
    public bool UsesPlayerWalkArt => isPlayerVisual && HasPlayerWalkFrames;
    public bool UsesDealerWalkArt => dealerWalkArt;
    public bool IsPlayerWalkSprite(Sprite sprite) =>
        HasPlayerWalkFrames && System.Array.IndexOf(playerWalkFrames, sprite) >= 0;
    public string PlayerFacingName => playerFacing.ToString();
    public Sprite SelectedStandingArt => UsesPlayerDirectionalArt ? PlayerFacingSprite :
        dealerWalkArt ? DealerSprite(PlayerFacing.Front, 2) :
        vip ? approvedVipStanding : approvedStanding;
    public bool IsPlayerWalking { get; private set; }
    public int PlayerWalkFrameIndex { get; private set; } = -1;
    public SpriteRenderer Renderer => spriteRenderer;
    public static float StandingHeight(CasinoAnimalSpecies species) => species switch
    {
        CasinoAnimalSpecies.Penguin => 2.2f,
        CasinoAnimalSpecies.Tiger => 2.2f,
        CasinoAnimalSpecies.Bear => 2.24f,
        CasinoAnimalSpecies.Fox => 2.2f,
        CasinoAnimalSpecies.Pig => 2.357143f,
        CasinoAnimalSpecies.Rabbit => 2.4f,
        CasinoAnimalSpecies.Dog => 2.5f,
        CasinoAnimalSpecies.Cat => 2.3f,
        CasinoAnimalSpecies.Raccoon => 2.5f,
        CasinoAnimalSpecies.Deer => 2.7f,
        CasinoAnimalSpecies.Elephant => 3.15f,
        _ => throw new System.ArgumentOutOfRangeException(nameof(species))
    };

    public void ConfigureApprovedArt(Sprite standingSprite, Sprite vipSprite)
    {
        if (standingSprite == null) throw new System.ArgumentNullException(nameof(standingSprite));
        approvedStanding = standingSprite;
        approvedVipStanding = vipSprite;
        BindAnimationFrames();
        ApplyStandingArt();
    }

    private bool HasPlayerWalkFrames => HasCompleteWalkCycle(playerWalkFrames);

    private static bool HasCompleteWalkCycle(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length != DirectionsPerPose * PlayerWalkFramesPerDirection)
            return false;
        foreach (Sprite sprite in sprites)
            if (sprite == null) return false;
        return true;
    }

    private void ApplyStandingArt() => ApplyArtSprite(SelectedStandingArt);

    private void ApplyArtSprite(Sprite selected)
    {
        if (selected == null)
            throw new System.InvalidOperationException($"Missing approved {(vip ? "VIP" : "standing")} art on {name}.");
        if (spriteRenderer.sprite != selected)
        {
            spriteRenderer.sprite = selected;
            spriteRenderer.GetComponent<PixelSpriteTextureBinding>()?.RefreshSprite();
        }
        float scale = StandingHeight(GetComponent<CasinoCharacterVisual>().Species) / selected.bounds.size.y;
        Transform parent = spriteRenderer.transform.parent;
        Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
        spriteRenderer.transform.localScale = new Vector3(
            scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.x)),
            scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.y)),
            scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.z)));
    }
    public void ConfigureVipAnimation(Texture2D texture) => vipAnimationAtlas = texture;
    public void ConfigureCustomerAnimation(Texture2D texture) => customerAnimationAtlas = texture;
    public bool UsesCustomerAnimation => customer != null && (animationAtlas == customerAnimationAtlas || animationAtlas == vipAnimationAtlas);
    public bool UsesVipAnimation => vip && vipAnimationAtlas != null && animationAtlas == vipAnimationAtlas;
    private static readonly Dictionary<Texture2D, Sprite[]> AnimationCache = new Dictionary<Texture2D, Sprite[]>();
    private static readonly Dictionary<Sprite, Sprite> StandingAuraCache = new Dictionary<Sprite, Sprite>();
    private static Sprite vipAura;
    private SpriteRenderer auraRenderer;
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
    public int CurrentFrame { get; private set; }
    public Vector3 HeadPosition => spriteRenderer.transform.TransformPoint(
        new Vector3(spriteRenderer.sprite.bounds.center.x, spriteRenderer.sprite.bounds.max.y, 0));
    public bool IsVip => vip;
    public void ConfigureAnimation(Texture2D texture) => animationAtlas = texture;
    private float reactionRemaining;
    private int reaction;
    private float nextIdleBeat = -1;
    public int IdleBeatsPlayed { get; private set; }
    public void ConfigureExpressions(Sprite[] sprites) => expressions = sprites;
    public void SetVip(bool value)
    {
        if (value && (UsesApprovedStandingArt ? approvedVipStanding == null : vipAnimationAtlas == null))
            throw new System.InvalidOperationException("VIP customers require their formalwear artwork.");
        vip = value;
        if (spriteRenderer == null) return;
        if (value) EnsureVipAccessories();
        if (auraRenderer != null) auraRenderer.enabled = value;
        animationAtlas = value ? vipAnimationAtlas : customerAnimationAtlas;
        BindAnimationFrames();
        if (UsesApprovedStandingArt) ApplyStandingArt();
        spriteRenderer.color = spriteColor;
    }
    public Sprite TitleFrame(float elapsed)
    {
        if (UsesApprovedStandingArt) return SelectedStandingArt;
        if (animationFrames != null)
            return AnimationSprite(CasinoAnimationPose.Idle, 0,
                PlaybackFrame(elapsed, 3.2f, true));
        if (expressions != null && expressions.Length == 4 && elapsed % 4 > 3) return expressions[0];
        return frames != null && frames.Length == 8 ? frames[Mathf.FloorToInt(elapsed * 6) % 2] : null;
    }
    public Sprite ReplayFrame(CasinoAnimationPose pose, int frame) =>
        animationFrames != null
            ? AnimationSprite(pose, 0, Mathf.Clamp(frame, 0, AnimationFramesPerDirection - 1))
            : TitleFrame(frame / 60f);
    public Sprite TitlePenguinFrame(bool facingDoor, bool walking, float elapsed)
    {
        if (UsesPlayerDirectionalArt)
        {
            PlayerFacing facing = facingDoor ? PlayerFacing.Back : PlayerFacing.Front;
            return walking && HasPlayerWalkFrames
                ? PlayerWalkSprite(facing, PlayerWalkPhase(elapsed))
                : PlayerIdleSprite(facing);
        }
        return facingDoor && animationFrames != null
            ? AnimationSprite(walking ? CasinoAnimationPose.Walk : CasinoAnimationPose.Idle, 1,
                PlaybackFrame(elapsed, walking ? .9f : 3.2f, true))
            : TitleFrame(elapsed);
    }
    public void React(bool winning)
    {
        reaction = winning ? 0 : 1;
        reactionRemaining = 2f;
        actionRemaining = 0;
        poseElapsed = 0;
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

    public static bool IsLooping(CasinoAnimationPose pose) =>
        pose == CasinoAnimationPose.Idle || pose == CasinoAnimationPose.Walk ||
        pose == CasinoAnimationPose.Tired || pose == CasinoAnimationPose.Rest ||
        pose == CasinoAnimationPose.Carry || pose == CasinoAnimationPose.Examine ||
        pose == CasinoAnimationPose.SeatedIdle;

    /// <summary>Poses a seated Roulette customer may legitimately show (never the card-reading Examine pose).</summary>
    public static bool IsSeatedRoulettePose(CasinoAnimationPose pose) =>
        pose == CasinoAnimationPose.SeatedIdle || pose == CasinoAnimationPose.Anticipate ||
        pose == CasinoAnimationPose.Present || pose == CasinoAnimationPose.Celebrate ||
        pose == CasinoAnimationPose.Disappointed || pose == CasinoAnimationPose.Sit;

    public static int PlaybackFrame(float elapsed, float duration, bool loop)
    {
        float progress = Mathf.Max(0, elapsed) / Mathf.Max(.01f, duration);
        if (loop) progress -= Mathf.Floor(progress);
        return Mathf.Clamp(Mathf.FloorToInt(progress * AnimationFramesPerDirection), 0,
            AnimationFramesPerDirection - 1);
    }

    private Sprite AnimationSprite(CasinoAnimationPose pose, int direction, int frame)
    {
        int index = ((int)pose * DirectionsPerPose + direction) * AnimationFramesPerDirection + frame;
        if (animationFrames[index] == null)
            animationFrames[index] = Sprite.Create(animationAtlas,
                new Rect(frame * 48, ((int)pose * DirectionsPerPose + direction) * 64, 48, 64),
                new Vector2(.5f, 0), 30, 0, SpriteMeshType.FullRect);
        return animationFrames[index];
    }

    private void Awake()
    {
        agent = GetComponentInParent<NavMeshAgent>();
        dealer = GetComponentInParent<AIDealer>();
        dealerWalkArt = dealer != null && HasCompleteWalkCycle(dealerWalkFrames);
        customer = GetComponentInParent<CasinoCustomerAI>();
        playerController = GetComponentInParent<CasinoPlayerController>();
        if (customer != null && customerAnimationAtlas != null) animationAtlas = customerAnimationAtlas;
        carrier = GetComponentInParent<CasinoPlayerFloatCarrier>();
        restScale = spriteRenderer != null ? spriteRenderer.transform.localScale : Vector3.one;
        if (spriteRenderer != null)
        {
            spriteColor = spriteRenderer.color;
            normalSortingOrder = spriteRenderer.sortingOrder;
        }
        previousPosition = transform.position;
        BindAnimationFrames();
        if (spriteRenderer != null && (UsesPlayerDirectionalArt || dealerWalkArt) && UsesApprovedStandingArt)
            ApplyStandingArt();
    }

    private void BindAnimationFrames()
    {
        if (UsesApprovedStandingArt)
        {
            animationFrames = null;
            return;
        }
        if (animationAtlas != null)
        {
            if (!AnimationCache.TryGetValue(animationAtlas, out animationFrames))
            {
                animationFrames = new Sprite[PoseCount * DirectionsPerPose * AnimationFramesPerDirection];
                AnimationCache.Add(animationAtlas, animationFrames);
            }
        }
    }

    private void EnsureVipAccessories()
    {
        if (UsesApprovedStandingArt)
        {
            if (!StandingAuraCache.TryGetValue(approvedVipStanding, out Sprite outline))
            {
                outline = CreateStandingAura(approvedVipStanding);
                StandingAuraCache.Add(approvedVipStanding, outline);
            }
            if (auraRenderer == null)
                auraRenderer = CreateAccessory("VIP Golden Aura", outline, new Vector3(0, 0, .01f), -1);
            else
                auraRenderer.sprite = outline;
            auraRenderer.sharedMaterial = spriteRenderer.sharedMaterial;
            PixelSpriteTextureBinding binding = auraRenderer.GetComponent<PixelSpriteTextureBinding>();
            if (binding == null) binding = auraRenderer.gameObject.AddComponent<PixelSpriteTextureBinding>();
            binding.RefreshSprite();
            return;
        }
        if (vipAura == null) CreateVipSprites();
        if (auraRenderer == null)
            auraRenderer = CreateAccessory("VIP Golden Aura", vipAura, new Vector3(0, 1f, .03f), -1);
    }

    private static Sprite CreateStandingAura(Sprite standingSprite)
    {
        const int radius = 4;
        Rect rect = standingSprite.rect;
        int sourceWidth = (int)rect.width, sourceHeight = (int)rect.height;
        int width = sourceWidth + radius * 2, height = sourceHeight + radius * 2;
        Color[] source = standingSprite.texture.GetPixels((int)rect.x, (int)rect.y, sourceWidth, sourceHeight);
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int sx = x - radius, sy = y - radius;
            float alpha = 0;
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int px = sx + dx, py = sy + dy;
                if (px < 0 || px >= sourceWidth || py < 0 || py >= sourceHeight) continue;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float falloff = Mathf.Clamp01(1 - distance / (radius + .5f));
                alpha = Mathf.Max(alpha, source[py * sourceWidth + px].a * falloff * falloff);
            }
            float inside = sx >= 0 && sx < sourceWidth && sy >= 0 && sy < sourceHeight
                ? source[sy * sourceWidth + sx].a : 0;
            pixels[y * width + x] = new Color(1f, .72f, .12f, alpha * (1 - inside));
        }
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = standingSprite.name + " Golden Glow",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        texture.Apply();
        Sprite aura = Sprite.Create(texture, new Rect(0, 0, width, height),
            new Vector2((standingSprite.pivot.x + radius) / width,
                (standingSprite.pivot.y + radius) / height),
            standingSprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        aura.name = texture.name;
        aura.hideFlags = HideFlags.HideAndDontSave;
        return aura;
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
        Texture2D glow = new Texture2D(64, 80, TextureFormat.RGBA32, false);
        glow.filterMode = FilterMode.Point;
        Color32[] pixels = new Color32[64 * 80];
        for (int y = 0; y < 80; y++)
        for (int x = 0; x < 64; x++)
        {
            float radius = new Vector2((x - 31.5f) / 32f, (y - 39.5f) / 40f).magnitude;
            float intensity = Mathf.Clamp01(1 - radius);
            pixels[y * 64 + x] = new Color(1f, .72f, .16f, intensity * intensity * .7f);
        }
        glow.SetPixels32(pixels);
        glow.Apply(false, true);
        glow.hideFlags = HideFlags.HideAndDontSave;
        vipAura = Sprite.Create(glow, new Rect(0, 0, 64, 80), new Vector2(.5f, .5f), 24);
        vipAura.hideFlags = HideFlags.HideAndDontSave;
    }

    // Seated customers are never frozen: content guests lean in to watch the game, while impatient ones
    // sigh and fidget more often. Timing is randomised per character so a table never moves in unison.
    private void UpdateSeatedPersonality(Transform actor)
    {
        if (nextIdleBeat < 0) nextIdleBeat = Time.time + Random.Range(3f, 8f);
        if (Time.time < nextIdleBeat || actionRemaining > 0 || reactionRemaining > 0 || gestureRemaining > 0 ||
            currentPose == CasinoAnimationPose.Sit) return;
        bool impatient = !customer.IsInService && customer.PatienceNormalized < .35f;
        nextIdleBeat = Time.time + (impatient ? Random.Range(2.4f, 4.2f) : Random.Range(6f, 12f));
        Vector3 ahead = actor.position + actor.forward;
        if (impatient) PlayAction(CasinoAnimationPose.Disappointed, ahead, Random.Range(.8f, 1.1f));
        else if (Random.value < .55f) PlayAction(CasinoAnimationPose.Anticipate, ahead, Random.Range(.8f, 1.4f));
        else return;
        IdleBeatsPlayed++;
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
        Vector3 displacement = actor.position - previousPosition;
        Vector3 velocity = agent != null && agent.enabled && agent.isOnNavMesh
            ? agent.velocity
            : hasPreviousPosition && Time.deltaTime > 0f ? displacement / Time.deltaTime : Vector3.zero;
        if (isPlayerVisual || dealerWalkArt) velocity.y = 0f;
        float speed = velocity.magnitude;
        previousPosition = actor.position;
        hasPreviousPosition = true;
        bool seated = customer != null && customer.IsSeated || dealer != null && dealer.IsResting;
        bool walking = speed > 0.12f && !seated;
        if (UsesPlayerDirectionalArt && walking)
        {
            Vector2 input = playerController.MoveInput;
            playerFacing = input == Vector2.zero
                ? FacingForHeading(displacement)
                : FacingForInput(input);
        }
        bool continueWalk = isPlayerVisual ? IsPlayerWalking : !dealerWalkArt || dealerWasWalking;
        walkTime = walking && continueWalk ? walkTime + Time.deltaTime : 0f;
        dealerWasWalking = dealerWalkArt && walking;
        IsPlayerWalking = isPlayerVisual && walking;
        PlayerWalkFrameIndex = -1;
        CasinoAnimationPose seatedPose = customer != null && customer.Seat != null &&
            customer.Seat.GameType == CasinoCustomerGame.Roulette ? CasinoAnimationPose.SeatedIdle : CasinoAnimationPose.Examine;
        CasinoAnimationPose gesturePose = gesture == CasinoGesture.Deal ? CasinoAnimationPose.Deal :
            gesture == CasinoGesture.Pay ? CasinoAnimationPose.Pay : CasinoAnimationPose.Collect;
        CasinoAnimationPose pose = carrier != null && carrier.IsCarrying ? CasinoAnimationPose.Carry :
            walking ? CasinoAnimationPose.Walk :
            reactionRemaining > 0 ? reaction == 0 ? CasinoAnimationPose.Celebrate : CasinoAnimationPose.Disappointed :
            gestureRemaining > 0 ? gesturePose :
            dealer != null && dealer.IsResting ? CasinoAnimationPose.Rest :
            dealer != null && dealer.NeedsBreak ? CasinoAnimationPose.Tired :
            seated ? seatedPose : CasinoAnimationPose.Idle;
        if (seated && customer != null) UpdateSeatedPersonality(actor);
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
        if (walking && Time.time >= nextFootstep)
        {
            nextFootstep = Time.time + .36f;
            CasinoSoundscape.Play(CasinoSoundCue.Step, actor.position);
        }

        if (!UsesApprovedStandingArt)
            spriteRenderer.sprite = frames[FrameIndex(actor.forward, walking, walkTime)];
        reactionRemaining = Mathf.Max(0, reactionRemaining - Time.deltaTime);
        if (!UsesApprovedStandingArt && !walking && expressions != null && expressions.Length == 4)
        {
            int index = reactionRemaining > 0 ? reaction : dealer != null && (dealer.NeedsBreak || dealer.IsResting) ? 2 :
                gestureRemaining > 0 ? 3 : -1;
            if (index >= 0) spriteRenderer.sprite = expressions[index];
        }
        if (animationFrames != null)
        {
            int direction = (gestureRemaining > 0 || actionRemaining > 0) && !walking ? gestureDirection : Direction(actor.forward);
            bool action = actionRemaining > 0 && !walking;
            bool gesturing = !action && gestureRemaining > 0 && !walking && currentPose == gesturePose;
            float duration = action ? actionDuration : gesturing ? gestureLength :
                currentPose == CasinoAnimationPose.Sit ? .8f :
                walking ? .8f : currentPose == CasinoAnimationPose.Idle ? 3.2f :
                currentPose == CasinoAnimationPose.Rest ? 4f : 2f;
            float elapsed = action ? actionDuration - actionRemaining :
                gesturing ? gestureLength - gestureRemaining : poseElapsed;
            int frame = PlaybackFrame(elapsed, duration, !action && !gesturing && IsLooping(currentPose));
            if (currentPose == CasinoAnimationPose.Carry && !walking) frame = 0;
            if (currentPose == CasinoAnimationPose.Sit && standing)
                frame = AnimationFramesPerDirection - 1 - frame;
            CurrentFrame = frame;
            spriteRenderer.sprite = AnimationSprite(currentPose, direction, frame);
        }
        spriteRenderer.transform.rotation = camera.transform.rotation;
        spriteRenderer.transform.localScale = restScale;
        // Select before PixelSpriteTextureBinding.LateUpdate, including after VIP/action changes.
        if (UsesApprovedStandingArt)
        {
            Sprite selected = SelectedStandingArt;
            if (isPlayerVisual && walking && HasPlayerWalkFrames)
            {
                int phase = PlayerWalkPhase(walkTime);
                PlayerWalkFrameIndex = (int)playerFacing * PlayerWalkFramesPerDirection + phase;
                selected = PlayerWalkSprite(playerFacing, phase);
            }
            else if (dealerWasWalking)
            {
                selected = DealerSprite(FacingForHeading(velocity), PlayerWalkPhase(walkTime));
            }
            ApplyArtSprite(selected);
        }
        if (auraRenderer != null && vip)
        {
            auraRenderer.transform.localScale = UsesApprovedStandingArt ? Vector3.one :
                Vector3.one * (1f + .035f * Mathf.Sin(Time.time * 2f));
            auraRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
            auraRenderer.color = new Color(1, 1, 1, UsesApprovedStandingArt ?
                .9f + .06f * Mathf.Sin(Time.time * 2f) : .8f + .2f * Mathf.Sin(Time.time * 2f));
        }
        poseElapsed += Time.deltaTime;
        gestureRemaining = Mathf.Max(0, gestureRemaining - Time.deltaTime);
        actionRemaining = Mathf.Max(0, actionRemaining - Time.deltaTime);
        spriteRenderer.transform.localPosition = new Vector3(0, .04f, 0);
        if (dealer != null && dealer.IsAtStation)
        {
            // The table's pixel overlay is transparent, so the depth buffer alone cannot hide feet.
            spriteRenderer.sortingOrder = dealer.AssignedStation.DealerBackdropOrder(normalSortingOrder);
            spriteRenderer.transform.position += dealer.AssignedStation.DealerPosition.forward * .36f;
        }
        else if (dealer != null)
        {
            spriteRenderer.sortingOrder = normalSortingOrder;
        }
    }

    private static int PlayerWalkPhase(float elapsed) =>
        Mathf.FloorToInt(elapsed / PlayerWalkFrameDuration) % PlayerWalkFramesPerDirection;

    private Sprite PlayerWalkSprite(PlayerFacing facing, int phase) =>
        playerWalkFrames[(int)facing * PlayerWalkFramesPerDirection + phase];

    private Sprite DealerSprite(PlayerFacing facing, int phase) =>
        dealerWalkFrames[(int)facing * PlayerWalkFramesPerDirection + phase];

    private Sprite PlayerFacingSprite => PlayerIdleSprite(playerFacing);

    private Sprite PlayerIdleSprite(PlayerFacing facing) => facing switch
    {
        PlayerFacing.Front => playerFront,
        PlayerFacing.Right => playerRight,
        PlayerFacing.Back => playerBack,
        PlayerFacing.Left => playerLeft,
        _ => playerFront
    };

    private PlayerFacing FacingForInput(Vector2 input)
    {
        if (input.x == 0) return input.y > 0 ? PlayerFacing.Back : PlayerFacing.Front;
        if (input.y == 0) return input.x > 0 ? PlayerFacing.Right : PlayerFacing.Left;
        // Keep the current facing if it is one of the held directions.
        PlayerFacing horizontal = input.x > 0 ? PlayerFacing.Right : PlayerFacing.Left;
        PlayerFacing vertical = input.y > 0 ? PlayerFacing.Back : PlayerFacing.Front;
        return playerFacing == horizontal ? horizontal : vertical;
    }

    private static PlayerFacing FacingForHeading(Vector3 heading)
    {
        if (Mathf.Abs(heading.x) > Mathf.Abs(heading.z))
            return heading.x > 0 ? PlayerFacing.Right : PlayerFacing.Left;
        return heading.z > 0 ? PlayerFacing.Back : PlayerFacing.Front;
    }
}
