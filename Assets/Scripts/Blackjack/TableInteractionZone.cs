using UnityEngine;

[DisallowMultipleComponent]
public sealed class TableInteractionZone : MonoBehaviour
{
    [SerializeField] private BoxCollider footprint;
    [SerializeField, Min(0f)] private float interactionDistance = 1.25f;
    [SerializeField, Min(0f)] private float sightHeight = 0.9f;

    public bool IsConfigured => footprint != null;

    public void Configure(BoxCollider tableFootprint, float distance = 1.25f)
    {
        footprint = tableFootprint;
        interactionDistance = Mathf.Max(0f, distance);
    }

    public bool IsPlayerInRange(Transform player)
    {
        if (player == null || footprint == null || !footprint.enabled || !gameObject.activeInHierarchy)
        {
            return false;
        }

        BoxCollider box = footprint;
        Vector3 boxCenter = box.transform.TransformPoint(box.center);
        Vector3 localPlayer = box.transform.InverseTransformPoint(
            new Vector3(player.position.x, boxCenter.y, player.position.z));
        Vector3 halfSize = box.size * 0.5f;
        float minX = box.center.x - halfSize.x;
        float maxX = box.center.x + halfSize.x;
        float minZ = box.center.z - halfSize.z;
        float maxZ = box.center.z + halfSize.z;
        Vector2 playerFloor = new Vector2(player.position.x, player.position.z);
        for (int edge = 0; edge < 4; edge++)
        {
            float edgeX = edge == 0
                ? minX
                : edge == 1
                    ? maxX
                    : Mathf.Clamp(localPlayer.x, minX, maxX);
            float edgeZ = edge == 2
                ? minZ
                : edge == 3
                    ? maxZ
                    : Mathf.Clamp(localPlayer.z, minZ, maxZ);
            Vector3 worldEdge = box.transform.TransformPoint(new Vector3(edgeX, box.center.y, edgeZ));
            Vector2 edgeFloor = new Vector2(worldEdge.x, worldEdge.z);
            if (Vector2.Distance(playerFloor, edgeFloor) <= interactionDistance &&
                HasUnobstructedPath(player, worldEdge))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasUnobstructedPath(Transform player, Vector3 worldEdge)
    {
        Vector3 rayStart = player.position + Vector3.up * sightHeight;
        Vector3 rayEnd = new Vector3(worldEdge.x, rayStart.y, worldEdge.z);
        Vector3 ray = rayEnd - rayStart;
        if (ray.sqrMagnitude <= Mathf.Epsilon)
        {
            return true;
        }

        RaycastHit[] hits = Physics.RaycastAll(rayStart, ray.normalized, ray.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.collider.transform;
            if (hit.collider == footprint || hitTransform == transform || hitTransform.IsChildOf(transform) ||
                hitTransform == player || hitTransform.IsChildOf(player))
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
