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
    [SerializeField, Min(0f)] private float blackjack1SelectionWeight = 1f;
    [SerializeField, Min(0f)] private float blackjack2SelectionWeight = 1f;
    [SerializeField, Min(0f)] private float rouletteSelectionWeight = 1f;

    private readonly HashSet<CasinoCustomerAI> activeCustomers = new HashSet<CasinoCustomerAI>();
    [SerializeField] private Transform entrance;
    [SerializeField] private Transform exit;
    [SerializeField] private Transform cashierWaypoint;
    [SerializeField] private CasinoSeat[] seats;
    [SerializeField] private GameObject[] customerCharacterPrefabs;
    [SerializeField] private CasinoCashDeskInteraction cashDesk;
    private float spawnTimer;
    private int nextCustomerNumber = 1;

    public float PatienceSeconds => patienceSeconds;
    public Transform Entrance => entrance;
    public Transform CashierWaypoint => cashierWaypoint;
    public int MinimumRoundsPerVisit => minimumRoundsPerVisit;
    public int MaximumRoundsPerVisit => Mathf.Max(minimumRoundsPerVisit, maximumRoundsPerVisit);
    public float LeaveAfterVisitChance => leaveAfterVisitChance;
    public float CashierAfterVisitChance => cashierAfterVisitChance;
    public float GetGameSelectionWeight(CasinoCustomerGame game) => game switch
    {
        CasinoCustomerGame.Blackjack1 => blackjack1SelectionWeight,
        CasinoCustomerGame.Blackjack2 => blackjack2SelectionWeight,
        CasinoCustomerGame.Roulette => rouletteSelectionWeight,
        _ => 0f
    };
    public float Blackjack1SelectionWeight => blackjack1SelectionWeight;
    public float Blackjack2SelectionWeight => blackjack2SelectionWeight;
    public float RouletteSelectionWeight => rouletteSelectionWeight;
    public int InitialCustomerCount => initialCustomerCount;
    public int ActiveCustomerCount => activeCustomers.Count;
    public bool IsConfigured => entrance != null && exit != null && cashierWaypoint != null &&
                                seats != null && customerCharacterPrefabs != null &&
                                customerCharacterPrefabs.Length > 0 &&
                                cashDesk != null && cashDesk.IsConfigured;

    public void Configure(
        Transform entrancePoint,
        Transform exitPoint,
        Transform cashier,
        CasinoSeat[] casinoSeats,
        GameObject[] characterPrefabs,
        CasinoCashDeskInteraction casinoCashDesk = null)
    {
        entrance = entrancePoint;
        exit = exitPoint;
        cashierWaypoint = cashier;
        seats = casinoSeats;
        customerCharacterPrefabs = characterPrefabs;
        cashDesk = casinoCashDesk;
    }

    public static bool TryChooseWeightedGame(
        IReadOnlyList<CasinoCustomerGame> availableGames,
        IReadOnlyList<float> weights,
        float randomValue,
        out CasinoCustomerGame selectedGame)
    {
        selectedGame = default;
        if (availableGames == null || weights == null || availableGames.Count != weights.Count ||
            float.IsNaN(randomValue) || float.IsInfinity(randomValue))
        {
            return false;
        }

        float totalWeight = 0f;
        for (int i = 0; i < weights.Count; i++)
        {
            totalWeight += Mathf.Max(0f, weights[i]);
        }

        if (totalWeight <= 0f)
        {
            return false;
        }

        float selection = Mathf.Clamp01(randomValue) * totalWeight;
        int lastPositiveWeightIndex = -1;
        for (int i = 0; i < availableGames.Count; i++)
        {
            float weight = Mathf.Max(0f, weights[i]);
            if (weight <= 0f)
            {
                continue;
            }

            lastPositiveWeightIndex = i;
            if (selection < weight)
            {
                selectedGame = availableGames[i];
                return true;
            }

            selection -= weight;
        }

        if (lastPositiveWeightIndex < 0)
        {
            return false;
        }

        selectedGame = availableGames[lastPositiveWeightIndex];
        return true;
    }

    public void NotifyCashierArrival(CasinoCustomerAI customer)
    {
        if (cashDesk == null)
        {
            Debug.LogError("A customer reached the cashier, but no cash desk is configured.", this);
            return;
        }

        cashDesk.CustomerArrived(customer);
    }

    private void Start()
    {
        if (entrance == null || exit == null || cashierWaypoint == null ||
            seats == null || customerCharacterPrefabs == null ||
            customerCharacterPrefabs.Length == 0)
        {
            Debug.LogError($"{nameof(CasinoCustomerSpawner)} on {name} is missing its entrance, exit, cashier waypoint, seats, or animal character prefabs.", this);
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

        GameObject characterPrefab = customerCharacterPrefabs[
            Random.Range(0, customerCharacterPrefabs.Length)];
        CasinoCharacterVisual characterTemplate = characterPrefab != null
            ? characterPrefab.GetComponent<CasinoCharacterVisual>()
            : null;
        if (characterTemplate == null)
        {
            Debug.LogError("A customer character prefab is missing its CasinoCharacterVisual component.", this);
            Object.Destroy(customerObject);
            spawnTimer = 2f;
            return;
        }

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
        CasinoCharacterVisual visual = Object.Instantiate(characterPrefab, customerObject.transform)
            .GetComponent<CasinoCharacterVisual>();
        visual.name = "Customer Animal Visual";
        Renderer bodyRenderer = visual.OutfitRenderer;
        if (bodyRenderer == null)
        {
            Debug.LogError("A customer animal prefab is missing its outfit renderer.", visual);
            Object.Destroy(customerObject);
            spawnTimer = 2f;
            return;
        }

        customer.Configure(this, seats, exit, cashierWaypoint, visual.transform, bodyRenderer,
            color, wanderRadius, seatChoiceChance);
        activeCustomers.Add(customer);
    }
}
