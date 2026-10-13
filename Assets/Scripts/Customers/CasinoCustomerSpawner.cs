using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class CasinoCustomerSpawner : MonoBehaviour
{
    [SerializeField, Min(0)] private int initialCustomerCount = 4;
    [SerializeField, Min(1)] private int maximumCustomers = 8;
    [SerializeField, Min(1f)] private float patienceSeconds = 35f;
    [SerializeField, Min(1f)] private float wanderRadius = 7f;
    [SerializeField, Range(0f, 1f)] private float seatChoiceChance = 0.28f;
    [SerializeField, Min(1f)] private float spawnInterval = 12f;
    [SerializeField, Min(1)] private int minimumRoundsPerVisit = 2;
    [SerializeField, Min(1)] private int maximumRoundsPerVisit = 7;
    [SerializeField, Range(0f, 1f)] private float leaveAfterVisitChance = 0.35f;
    [SerializeField, Range(0f, 1f)] private float cashierAfterVisitChance = 0.25f;

    private readonly HashSet<CasinoCustomerAI> activeCustomers = new HashSet<CasinoCustomerAI>();
    [SerializeField] private Transform entrance;
    [SerializeField] private Transform exit;
    [SerializeField] private Transform cashierWaypoint;
    [SerializeField] private CasinoSeat[] seats;
    [SerializeField] private Material[] customerMaterials;
    private float spawnTimer;
    private int nextCustomerNumber = 1;

    public float PatienceSeconds => patienceSeconds;
    public Transform CashierWaypoint => cashierWaypoint;
    public int MinimumRoundsPerVisit => minimumRoundsPerVisit;
    public int MaximumRoundsPerVisit => Mathf.Max(minimumRoundsPerVisit, maximumRoundsPerVisit);
    public float LeaveAfterVisitChance => leaveAfterVisitChance;
    public float CashierAfterVisitChance => cashierAfterVisitChance;
    public int InitialCustomerCount => initialCustomerCount;
    public int ActiveCustomerCount => activeCustomers.Count;
    public bool IsConfigured => entrance != null && exit != null && cashierWaypoint != null &&
                                seats != null && customerMaterials != null && customerMaterials.Length > 0;

    public void Configure(
        Transform entrancePoint,
        Transform exitPoint,
        Transform cashier,
        CasinoSeat[] casinoSeats,
        Material[] materials)
    {
        entrance = entrancePoint;
        exit = exitPoint;
        cashierWaypoint = cashier;
        seats = casinoSeats;
        customerMaterials = materials;
    }

    private void Start()
    {
        if (entrance == null || exit == null || cashierWaypoint == null ||
            seats == null || customerMaterials == null || customerMaterials.Length == 0)
        {
            Debug.LogError($"{nameof(CasinoCustomerSpawner)} on {name} is missing its entrance, exit, cashier waypoint, seats, or character materials.", this);
            enabled = false;
            return;
        }

        for (int i = 0; i < initialCustomerCount; i++)
        {
            SpawnCustomer(i);
        }

        spawnTimer = spawnInterval;
    }

    private void Update()
    {
        if (activeCustomers.Count >= maximumCustomers)
        {
            return;
        }

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            SpawnCustomer(activeCustomers.Count * 2);
            spawnTimer = spawnInterval;
        }
    }

    public void NotifyDespawned(CasinoCustomerAI customer)
    {
        activeCustomers.Remove(customer);
    }

    private void SpawnCustomer(int entranceOffset)
    {
        Vector3 requestedPosition = entrance.position + new Vector3((entranceOffset % 4) * 0.52f, 0f, (entranceOffset / 4) * 0.55f);
        if (!NavMesh.SamplePosition(requestedPosition, out NavMeshHit spawnHit, 2f, NavMesh.AllAreas))
        {
            Debug.LogError($"No baked NavMesh found near casino entrance at {requestedPosition}.", this);
            spawnTimer = 2f;
            return;
        }

        GameObject customerObject = new GameObject($"Customer {nextCustomerNumber++:00}");
        customerObject.transform.position = spawnHit.position;
        NavMeshAgent agent = customerObject.AddComponent<NavMeshAgent>();
        agent.Warp(spawnHit.position);
        CustomerPatience patience = customerObject.AddComponent<CustomerPatience>();
        CasinoCustomerAI customer = customerObject.AddComponent<CasinoCustomerAI>();

        Color[] variations =
        {
            new Color(0.94f, 0.35f, 0.28f),
            new Color(0.27f, 0.66f, 0.86f),
            new Color(0.94f, 0.72f, 0.24f),
            new Color(0.56f, 0.4f, 0.8f),
            new Color(0.3f, 0.76f, 0.49f),
            new Color(0.92f, 0.46f, 0.7f)
        };
        Color color = variations[(nextCustomerNumber - 2) % variations.Length];
        Material material = customerMaterials[Random.Range(0, customerMaterials.Length)];

        GameObject visual = new GameObject("Customer Visual");
        visual.transform.SetParent(customerObject.transform, false);
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(visual.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.68f, 0f);
        body.transform.localScale = new Vector3(0.62f, 0.7f, 0.62f);
        Object.Destroy(body.GetComponent<Collider>());
        Renderer bodyRenderer = body.GetComponent<Renderer>();
        bodyRenderer.sharedMaterial = material;

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(visual.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.42f, 0f);
        head.transform.localScale = Vector3.one * 0.42f;
        Object.Destroy(head.GetComponent<Collider>());
        head.GetComponent<Renderer>().sharedMaterial = customerMaterials[(nextCustomerNumber - 2) % customerMaterials.Length];

        customer.Configure(this, seats, exit, cashierWaypoint, visual.transform, bodyRenderer, color, wanderRadius, seatChoiceChance);
        activeCustomers.Add(customer);
    }
}
