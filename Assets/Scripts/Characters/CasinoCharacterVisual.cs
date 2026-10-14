using UnityEngine;
using UnityEngine.AI;

public enum CasinoGesture
{
    Deal,
    Pay,
    Collect
}

[DisallowMultipleComponent]
public sealed class CasinoCharacterVisual : MonoBehaviour
{
    [SerializeField] private CasinoAnimalSpecies species;
    [SerializeField] private Transform animatedBody;
    [SerializeField] private Transform animatedHead;
    [SerializeField] private Transform animatedAccessory;
    [SerializeField] private Renderer outfitRenderer;
    [SerializeField] private Transform leftArm;
    [SerializeField] private Transform rightArm;

    private Vector3 bodyBasePosition;
    private Quaternion headBaseRotation;
    private Quaternion accessoryBaseRotation;
    private Vector3 previousPosition;
    private NavMeshAgent agent;
    private float animationPhase;
    private float baseBodyHeight;
    private bool initialized;
    private bool gestureActive;
    private CasinoGesture gesture;
    private float gestureTime;
    private float gestureDuration;
    private float gestureYaw;
    private float currentYaw;

    public CasinoAnimalSpecies Species => species;
    public Renderer OutfitRenderer => outfitRenderer;

    public void Configure(
        CasinoAnimalSpecies characterSpecies,
        Transform body,
        Transform head,
        Transform accessory,
        Renderer mainOutfitRenderer)
    {
        species = characterSpecies;
        animatedBody = body;
        animatedHead = head;
        animatedAccessory = accessory;
        outfitRenderer = mainOutfitRenderer;
        CaptureRestPose();
    }

    public void ConfigureArms(Transform left, Transform right)
    {
        leftArm = left;
        rightArm = right;
    }

    public void PlayGesture(CasinoGesture type, Vector3 worldTarget, float duration = 0.7f)
    {
        Vector3 direction = worldTarget - transform.position;
        direction.y = 0f;
        gestureYaw = direction.sqrMagnitude > 0.0001f
            ? Mathf.Clamp(Vector3.SignedAngle(transform.forward, direction, Vector3.up), -55f, 55f)
            : 0f;
        gesture = type;
        gestureDuration = Mathf.Max(0.2f, duration);
        gestureTime = 0f;
        gestureActive = true;
    }

    private void Awake()
    {
        agent = GetComponentInParent<NavMeshAgent>();
        CaptureRestPose();
    }

    private void LateUpdate()
    {
        if (animatedBody == null)
        {
            return;
        }

        Vector3 position = transform.parent != null ? transform.parent.position : transform.position;
        float speed = agent != null
            ? agent.velocity.magnitude
            : initialized && Time.deltaTime > 0f
                ? Vector3.Distance(position, previousPosition) / Time.deltaTime
                : 0f;
        previousPosition = position;
        initialized = true;

        bool walking = speed > 0.12f;
        float frequency = walking ? 11f : 2.8f;
        animationPhase += Time.deltaTime * frequency;
        float stride = walking ? Mathf.Sin(animationPhase) : Mathf.Sin(animationPhase) * 0.18f;
        float bounce = walking ? Mathf.Abs(Mathf.Sin(animationPhase)) * 0.045f : Mathf.Sin(animationPhase) * 0.012f;

        Vector3 bodyPosition = bodyBasePosition;
        bodyPosition.y = baseBodyHeight + bounce;
        animatedBody.localPosition = bodyPosition;
        float pose = 0f;
        if (gestureActive)
        {
            gestureTime += Time.deltaTime;
            float t = gestureTime / gestureDuration;
            pose = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            gestureActive = t < 1f;
        }

        currentYaw = Mathf.MoveTowards(currentYaw, gestureActive ? gestureYaw * Mathf.Min(1f, pose * 2f) : 0f,
            Time.deltaTime * 240f);
        animatedBody.localRotation = Quaternion.Euler(0f, currentYaw, walking ? stride * 5f : stride * 1.2f);
        ApplyArms(walking, stride, pose);

        if (animatedHead != null)
        {
            animatedHead.localRotation = headBaseRotation * Quaternion.Euler(walking ? -stride * 4f : stride * 1.5f, 0f, 0f);
        }

        if (animatedAccessory != null)
        {
            animatedAccessory.localRotation = accessoryBaseRotation * Quaternion.Euler(0f, 0f, walking ? stride * 9f : stride * 3f);
        }
    }

    private void ApplyArms(bool walking, float stride, float pose)
    {
        float left = walking ? stride * 28f : 0f;
        float right = walking ? -stride * 28f : 0f;
        if (gestureActive || pose > 0f)
        {
            float reach = -75f * pose;
            float flick = Mathf.Sin(gestureTime * 18f) * 8f * pose;
            switch (gesture)
            {
                case CasinoGesture.Deal:
                    right = reach + flick;
                    break;
                case CasinoGesture.Pay:
                    left = reach * 0.8f;
                    right = reach + flick;
                    break;
                default:
                    right = reach * 1.1f;
                    left = reach * 0.5f;
                    break;
            }
        }

        if (leftArm != null)
        {
            leftArm.localRotation = Quaternion.Euler(left, 0f, 0f);
        }

        if (rightArm != null)
        {
            rightArm.localRotation = Quaternion.Euler(right, 0f, 0f);
        }
    }

    private void CaptureRestPose()
    {
        if (animatedBody != null)
        {
            bodyBasePosition = animatedBody.localPosition;
            baseBodyHeight = bodyBasePosition.y;
        }

        if (animatedHead != null)
        {
            headBaseRotation = animatedHead.localRotation;
        }

        if (animatedAccessory != null)
        {
            accessoryBaseRotation = animatedAccessory.localRotation;
        }
    }
}
