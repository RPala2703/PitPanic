using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mirrors the active blackjack round on the table itself: cards, bet chips, payouts and collection,
/// with matching dealer gestures. It only reads round state and never changes gameplay.
/// </summary>
[DisallowMultipleComponent]
public sealed class BlackjackFloorActivity : MonoBehaviour
{
    private const float FeltHeight = 1.36f;
    private static readonly Vector3 ShoeLocal = new Vector3(-1.52f, 1.78f, 1.0f);
    private static readonly Vector3 TrayLocal = new Vector3(1.70f, 1.62f, 0.88f);
    private static readonly Vector3 DiscardLocal = new Vector3(1.15f, FeltHeight, 0.95f);

    [SerializeField] private BlackjackTableInteraction table;
    [SerializeField] private Material cardFaceMaterial;
    [SerializeField] private Material cardBackMaterial;
    [SerializeField] private Material redMarkMaterial;
    [SerializeField] private Material blackMarkMaterial;
    [SerializeField] private Material[] chipMaterials;

    private sealed class Mover
    {
        public Transform Transform;
        public Vector3 From;
        public Vector3 To;
        public Quaternion Rotation;
        public float Start;
        public float Duration;
        public bool DestroyOnArrival;
        public bool Arrived;
    }

    private sealed class CardVisual
    {
        public Mover Mover;
        public Renderer Face;
        public GameObject Mark;
        public bool FaceDown;
    }

    private sealed class HandVisuals
    {
        public readonly List<CardVisual> Cards = new List<CardVisual>();
        public readonly List<Mover> Chips = new List<Mover>();
        public bool BetPlaced;
        public bool PayoutShown;
        public bool BetCollected;
    }

    private readonly Dictionary<BlackjackHand, HandVisuals> hands = new Dictionary<BlackjackHand, HandVisuals>();
    private readonly List<CardVisual> dealerCards = new List<CardVisual>();
    private readonly List<Mover> movers = new List<Mover>();
    private BlackjackTableRound trackedRound;
    private Transform container;
    private bool holeRevealed;
    private bool cleared;

    public int VisibleCardCount
    {
        get
        {
            int count = dealerCards.Count;
            foreach (HandVisuals visuals in hands.Values)
            {
                count += visuals.Cards.Count;
            }

            return count;
        }
    }

    public void Configure(
        BlackjackTableInteraction blackjackTable,
        Material face,
        Material back,
        Material redMark,
        Material blackMark,
        Material[] chips)
    {
        table = blackjackTable;
        cardFaceMaterial = face;
        cardBackMaterial = back;
        redMarkMaterial = redMark;
        blackMarkMaterial = blackMark;
        chipMaterials = chips;
    }

    private void Awake()
    {
        container = new GameObject("Table Activity Props").transform;
        container.SetParent(transform, false);
    }

    private void Update()
    {
        if (table == null)
        {
            return;
        }

        BlackjackTableRound round = table.ActiveRound;
        if (round != trackedRound)
        {
            ResetVisuals();
            trackedRound = round;
        }

        if (round != null)
        {
            Sync(round, table.State);
        }

        AdvanceMovers();
    }

    private void Sync(BlackjackTableRound round, BlackjackTableState state)
    {
        bool revealHole = state == BlackjackTableState.DealerTurn ||
                          state == BlackjackTableState.Settlement ||
                          state == BlackjackTableState.ClearingTable;

        for (int h = 0; h < round.CustomerHands.Count; h++)
        {
            BlackjackHand hand = round.CustomerHands[h];
            if (!hands.TryGetValue(hand, out HandVisuals visuals))
            {
                visuals = new HandVisuals();
                hands[hand] = visuals;
            }

            if (!visuals.BetPlaced && hand.BetPence > 0)
            {
                visuals.BetPlaced = true;
                SpawnBetStack(hand, visuals);
            }

            while (visuals.Cards.Count < hand.Cards.Count)
            {
                int index = visuals.Cards.Count;
                Vector3 target = CardTarget(HandAnchor(hand), index);
                visuals.Cards.Add(SpawnCard(hand.Cards[index], target, false));
                Gesture(CasinoGesture.Deal, container.TransformPoint(target));
            }

            if (hand.PayoutPaid && !visuals.PayoutShown)
            {
                visuals.PayoutShown = true;
                SpawnPayout(hand, visuals);
            }

            if (state == BlackjackTableState.Settlement && !visuals.BetCollected &&
                hand.Outcome == BlackjackOutcome.Lose && !hand.PayoutPaid)
            {
                visuals.BetCollected = true;
                CollectChips(visuals);
            }
        }

        while (dealerCards.Count < round.DealerCards.Count)
        {
            int index = dealerCards.Count;
            Vector3 target = DealerCardTarget(index);
            bool hidden = index == 1 && !revealHole;
            dealerCards.Add(SpawnCard(round.DealerCards[index], target, hidden));
            Gesture(CasinoGesture.Deal, container.TransformPoint(target));
        }

        if (revealHole && !holeRevealed && dealerCards.Count > 1)
        {
            holeRevealed = true;
            RevealCard(dealerCards[1]);
        }

        if (state == BlackjackTableState.ClearingTable && !cleared)
        {
            cleared = true;
            foreach (HandVisuals visuals in hands.Values)
            {
                CollectChips(visuals);
                foreach (CardVisual card in visuals.Cards)
                {
                    SendToDiscard(card.Mover);
                }
            }

            foreach (CardVisual card in dealerCards)
            {
                SendToDiscard(card.Mover);
            }

            Gesture(CasinoGesture.Collect, container.TransformPoint(DiscardLocal), 0.9f);
        }
    }

    private Vector3 HandAnchor(BlackjackHand hand)
    {
        Vector3 local = hand.Seat != null && hand.Seat.BettingPosition != null
            ? transform.InverseTransformPoint(hand.Seat.BettingPosition.position)
            : Vector3.zero;
        local.y = FeltHeight;
        return local;
    }

    private static Vector3 CardTarget(Vector3 anchor, int index)
    {
        return new Vector3(anchor.x + (index - 0.5f) * 0.20f, FeltHeight + index * 0.004f, anchor.z + 0.58f);
    }

    private static Vector3 DealerCardTarget(int index)
    {
        return new Vector3(-0.22f + index * 0.30f, FeltHeight + index * 0.004f, 0.52f);
    }

    private CardVisual SpawnCard(BlackjackCard card, Vector3 targetLocal, bool faceDown)
    {
        GameObject cardObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cardObject.name = "Table Card";
        DestroyCollider(cardObject);
        cardObject.transform.SetParent(container, false);
        cardObject.transform.localScale = new Vector3(0.17f, 0.012f, 0.24f);
        Renderer renderer = cardObject.GetComponent<Renderer>();
        renderer.sharedMaterial = faceDown ? cardBackMaterial : cardFaceMaterial;

        GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mark.name = "Card Suit Mark";
        DestroyCollider(mark);
        mark.transform.SetParent(cardObject.transform, false);
        mark.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        mark.transform.localScale = new Vector3(0.45f, 0.2f, 0.34f);
        bool red = card.Suit == 1 || card.Suit == 2;
        mark.GetComponent<Renderer>().sharedMaterial = red ? redMarkMaterial : blackMarkMaterial;
        mark.SetActive(!faceDown);

        Mover mover = new Mover
        {
            Transform = cardObject.transform,
            From = ShoeLocal,
            To = targetLocal,
            Rotation = Quaternion.Euler(0f, (targetLocal.x * 7f) % 12f, 0f),
            Start = Time.time,
            Duration = 0.45f
        };
        cardObject.transform.localPosition = ShoeLocal;
        movers.Add(mover);
        return new CardVisual { Mover = mover, Face = renderer, Mark = mark, FaceDown = faceDown };
    }

    private void RevealCard(CardVisual card)
    {
        card.FaceDown = false;
        card.Face.sharedMaterial = cardFaceMaterial;
        card.Mark.SetActive(true);
    }

    private void SpawnBetStack(BlackjackHand hand, HandVisuals visuals)
    {
        Vector3 anchor = HandAnchor(hand);
        int count = Mathf.Clamp(hand.BetPence / 500, 1, 6);
        BuildChipStack(anchor, count, visuals.Chips, TrayLocal, 0);
        Gesture(CasinoGesture.Pay, container.TransformPoint(anchor), 0.5f);
    }

    private void SpawnPayout(BlackjackHand hand, HandVisuals visuals)
    {
        Vector3 anchor = HandAnchor(hand) + new Vector3(0.30f, 0f, 0f);
        int count = Mathf.Clamp(hand.PayoutPence / 500, 1, 6);
        BuildChipStack(anchor, count, visuals.Chips, TrayLocal, 1);
        Gesture(CasinoGesture.Pay, container.TransformPoint(anchor), 0.8f);
    }

    private void BuildChipStack(Vector3 anchor, int count, List<Mover> output, Vector3 origin, int materialShift)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject chip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            chip.name = "Table Chip";
            DestroyCollider(chip);
            chip.transform.SetParent(container, false);
            chip.transform.localScale = new Vector3(0.17f, 0.012f, 0.17f);
            int materialIndex = chipMaterials != null && chipMaterials.Length > 0
                ? (count + materialShift) % chipMaterials.Length
                : 0;
            if (chipMaterials != null && chipMaterials.Length > 0)
            {
                chip.GetComponent<Renderer>().sharedMaterial = chipMaterials[materialIndex];
            }

            chip.transform.localPosition = origin;
            Mover mover = new Mover
            {
                Transform = chip.transform,
                From = origin,
                To = anchor + new Vector3(0f, 0.012f + i * 0.026f, 0f),
                Rotation = Quaternion.identity,
                Start = Time.time + i * 0.07f,
                Duration = 0.4f
            };
            movers.Add(mover);
            output.Add(mover);
        }
    }

    private void CollectChips(HandVisuals visuals)
    {
        foreach (Mover chip in visuals.Chips)
        {
            SendToTray(chip);
        }

        visuals.Chips.Clear();
        Gesture(CasinoGesture.Collect, container.TransformPoint(TrayLocal), 0.8f);
    }

    private void SendToTray(Mover mover)
    {
        mover.From = mover.Transform.localPosition;
        mover.To = TrayLocal;
        mover.Start = Time.time;
        mover.Duration = 0.45f;
        mover.DestroyOnArrival = true;
        mover.Arrived = false;
    }

    private void SendToDiscard(Mover mover)
    {
        mover.From = mover.Transform.localPosition;
        mover.To = DiscardLocal;
        mover.Start = Time.time + 0.15f;
        mover.Duration = 0.5f;
        mover.DestroyOnArrival = true;
        mover.Arrived = false;
    }

    private void AdvanceMovers()
    {
        for (int i = movers.Count - 1; i >= 0; i--)
        {
            Mover mover = movers[i];
            if (mover.Transform == null)
            {
                movers.RemoveAt(i);
                continue;
            }

            if (Time.time < mover.Start)
            {
                continue;
            }

            float t = Mathf.Clamp01((Time.time - mover.Start) / mover.Duration);
            float eased = t * t * (3f - 2f * t);
            Vector3 position = Vector3.Lerp(mover.From, mover.To, eased);
            position.y += Mathf.Sin(t * Mathf.PI) * 0.18f;
            mover.Transform.localPosition = position;
            mover.Transform.localRotation = Quaternion.Slerp(Quaternion.identity, mover.Rotation, eased);
            if (t >= 1f)
            {
                mover.Arrived = true;
                if (mover.DestroyOnArrival)
                {
                    Destroy(mover.Transform.gameObject);
                }

                movers.RemoveAt(i);
            }
        }
    }

    private void Gesture(CasinoGesture gesture, Vector3 worldTarget, float duration = 0.6f)
    {
        AIDealer dealer = table != null ? table.AssignedDealer : null;
        if (dealer == null)
        {
            return;
        }

        CasinoCharacterVisual visual = dealer.GetComponentInChildren<CasinoCharacterVisual>();
        if (visual != null)
        {
            visual.PlayGesture(gesture, worldTarget, duration);
        }
    }

    private void ResetVisuals()
    {
        if (container != null)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Destroy(container.GetChild(i).gameObject);
            }
        }

        hands.Clear();
        dealerCards.Clear();
        movers.Clear();
        holeRevealed = false;
        cleared = false;
    }

    private static void DestroyCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
    }
}
