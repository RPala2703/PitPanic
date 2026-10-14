using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class CasinoCharacterVisual : MonoBehaviour
{
    [SerializeField] private CasinoAnimalSpecies species;
    [SerializeField] private Transform animatedBody;
    [SerializeField] private Transform animatedHead;
    [SerializeField] private Transform animatedAccessory;
    [SerializeField] private Renderer outfitRenderer;

    private Vector3 bodyBasePosition;
    private Quaternion headBaseRotation;
    private Quaternion accessoryBaseRotation;
    private Vector3 previousPosition;
    private NavMeshAgent agent;
    private float animationPhase;
    private float baseBodyHeight;
    private bool initialized;

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
        animatedBody.localRotation = Quaternion.Euler(0f, 0f, walking ? stride * 5f : stride * 1.2f);

        if (animatedHead != null)
        {
            animatedHead.localRotation = headBaseRotation * Quaternion.Euler(walking ? -stride * 4f : stride * 1.5f, 0f, 0f);
        }

        if (animatedAccessory != null)
        {
            animatedAccessory.localRotation = accessoryBaseRotation * Quaternion.Euler(0f, 0f, walking ? stride * 9f : stride * 3f);
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
