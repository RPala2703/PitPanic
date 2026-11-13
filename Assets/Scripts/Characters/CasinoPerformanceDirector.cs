using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Non-blocking choreography for multi-character moments (tips, reactions, glances). Gameplay decides what
/// happened; the director only schedules visual beats. Beats owned by destroyed objects are dropped, so an
/// interrupted performance can never stall a table or replay a tip.
/// </summary>
[DisallowMultipleComponent]
public sealed class CasinoPerformanceDirector : MonoBehaviour
{
    private sealed class Beat
    {
        public float Time;
        public UnityEngine.Object Owner;
        public bool Owned;
        public Action Action;
    }

    private sealed class Flight
    {
        public Transform Prop;
        public Vector3 From;
        public Vector3 To;
        public float Start;
        public float Duration;
        public float Arc;
    }

    private sealed class Fx
    {
        public Transform Transform;
        public Vector3 Velocity;
        public float Start;
        public float Duration;
        public SpriteRenderer Sprite;
        public CanvasGroup Group;
    }

    private static CasinoPerformanceDirector instance;
    private readonly List<Beat> beats = new List<Beat>();
    private readonly List<Flight> flights = new List<Flight>();
    private readonly List<Fx> effects = new List<Fx>();
    private Camera targetCamera;

    public static event Action<CasinoTipRecord> TipDelivered;
    public static int ActiveBeatCount => instance != null ? instance.beats.Count : 0;
    public static int ActivePropCount => instance != null ? instance.flights.Count + instance.effects.Count : 0;
    public static int TipsPerformed { get; private set; }

    private static CasinoPerformanceDirector Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("Casino Performance Director").AddComponent<CasinoPerformanceDirector>();
            return instance;
        }
    }

    /// <summary>Runs <paramref name="action"/> after a delay unless <paramref name="owner"/> has been destroyed.</summary>
    public static void After(float delay, UnityEngine.Object owner, Action action)
    {
        if (action == null) return;
        Instance.beats.Add(new Beat { Time = Time.time + Mathf.Max(0, delay), Owner = owner, Owned = owner != null, Action = action });
    }

    /// <summary>Plays a short reaction on a character after a delay, so a table reacts as a crowd, not in unison.</summary>
    public static void ReactLater(Component character, bool winning, float delay)
    {
        PixelCharacterVisual visual = character != null ? character.GetComponentInChildren<PixelCharacterVisual>() : null;
        if (visual == null) return;
        After(delay, visual, () => visual.React(winning));
    }

    public static void ActLater(Component character, CasinoAnimationPose pose, Vector3 target, float duration, float delay)
    {
        PixelCharacterVisual visual = character != null ? character.GetComponentInChildren<PixelCharacterVisual>() : null;
        if (visual == null) return;
        After(delay, visual, () => visual.PlayAction(pose, target, duration));
    }

    /// <summary>Seated customers at other nearby tables glance over when someone wins big.</summary>
    public static void CrowdNotices(CasinoCustomerAI winner, float radius = 6f)
    {
        if (winner == null) return;
        CasinoDealerStation winnerTable = winner.Seat != null ? winner.Seat.DealerStation : null;
        foreach (CasinoCustomerAI customer in FindObjectsByType<CasinoCustomerAI>(FindObjectsSortMode.None))
        {
            if (customer == winner || !customer.IsSeated || customer.Seat == null || customer.Seat.DealerStation == winnerTable ||
                (customer.transform.position - winner.transform.position).sqrMagnitude > radius * radius) continue;
            ActLater(customer, CasinoAnimationPose.Anticipate, winner.transform.position, .9f, .3f + UnityEngine.Random.value * .4f);
        }
    }

    /// <summary>
    /// The tip performance: the customer glances at the recipient, leans in, slides a real chip across the felt,
    /// the recipient catches it and says thanks. <paramref name="ready"/> lets a table finish its payout first.
    /// </summary>
    public static void PlayTip(CasinoTipRecord tip, Component recipient, Func<bool> ready, float maximumWait = 4f)
    {
        Instance.WaitThenTip(tip, recipient, ready, Time.time + maximumWait);
    }

    private void WaitThenTip(CasinoTipRecord tip, Component recipient, Func<bool> ready, float deadline)
    {
        bool isReady;
        try { isReady = ready == null || ready(); }
        catch (Exception) { isReady = true; }
        if (!isReady && Time.time < deadline)
        {
            After(.1f, null, () => WaitThenTip(tip, recipient, ready, deadline));
            return;
        }

        PerformTip(tip, recipient);
    }

    private void PerformTip(CasinoTipRecord tip, Component recipient)
    {
        TipsPerformed++;
        CasinoCustomerAI customer = tip.Customer;
        PixelCharacterVisual giver = customer != null ? customer.GetComponentInChildren<PixelCharacterVisual>() : null;
        PixelCharacterVisual taker = recipient != null ? recipient.GetComponentInChildren<PixelCharacterVisual>() : null;
        if (giver == null || taker == null)
        {
            // Someone already left the table; the tip still counts, only the hand-off is skipped.
            Deliver(tip, taker != null ? taker.transform.position : giver != null ? giver.transform.position : Vector3.zero);
            return;
        }

        Vector3 giverPosition = giver.transform.position;
        Vector3 takerPosition = taker.transform.position;
        Vector3 across = takerPosition - giverPosition;
        across.y = 0;
        across = across.sqrMagnitude > .001f ? across.normalized : Vector3.forward;

        // 1. Glance and anticipation: the customer looks over and leans in; the dealer notices.
        giver.PlayAction(CasinoAnimationPose.Anticipate, takerPosition, .5f);
        After(.25f, taker, () => taker.FaceGesture(giver.transform.position));
        // 2. Reach, pick up and slide: the chip leaves the hand at the arm's full extension.
        After(.5f, giver, () => giver.PlayAction(CasinoAnimationPose.Present, takerPosition, .95f));
        After(.92f, null, () =>
        {
            Vector3 from = (giver != null ? giver.transform.position : giverPosition) + Vector3.up * .95f + across * .32f;
            Vector3 to = (taker != null ? taker.transform.position : takerPosition) + Vector3.up * 1.05f - across * .3f;
            Transform chip = CreateBillboard("Tip Chip", CasinoPixelArt.ChipForValue(tip.AmountPence), from, .24f, 80).transform;
            flights.Add(new Flight { Prop = chip, From = from, To = to, Start = Time.time, Duration = .62f, Arc = .32f });
            if (tip.Vip) Sparkles(from, 5, .5f);
        });
        // 3. Catch and thanks: the recipient's hand closes on the chip as it lands.
        After(1.3f, taker, () =>
        {
            taker.PlayAction(CasinoAnimationPose.Collect, giverPosition, .55f);
            CasinoSoundscape.Play(CasinoSoundCue.Chip, taker.transform.position);
        });
        After(1.85f, taker, () =>
        {
            taker.PlayAction(CasinoAnimationPose.Present, giverPosition, .9f);
            CasinoSoundscape.Play(CasinoSoundCue.Tip, taker.transform.position);
        });
        After(1.9f, giver, () => giver.PlayAction(CasinoAnimationPose.Celebrate, takerPosition, .8f));
        After(1.6f, null, () => Deliver(tip, (taker != null ? taker.transform.position : takerPosition) + Vector3.up * 1.9f));
    }

    private void Deliver(CasinoTipRecord tip, Vector3 popupPosition)
    {
        Popup(popupPosition, $"+{CasinoTipJar.Format(tip.AmountPence)} TIP", tip.Vip);
        if (tip.Vip) Sparkles(popupPosition, 10, .9f);
        TipDelivered?.Invoke(tip);
    }

    public static void Sparkles(Vector3 position, int count, float spread)
    {
        CasinoPerformanceDirector director = Instance;
        for (int i = 0; i < count; i++)
        {
            float angle = (i + UnityEngine.Random.value * .6f) / count * Mathf.PI * 2;
            SpriteRenderer sparkle = director.CreateBillboard("VIP Tip Sparkle", CasinoPixelArt.Sparkle,
                position, UnityEngine.Random.Range(.12f, .2f), 82);
            sparkle.color = new Color(1f, .84f, .25f);
            director.effects.Add(new Fx
            {
                Transform = sparkle.transform,
                Sprite = sparkle,
                Velocity = new Vector3(Mathf.Cos(angle), 1.2f + UnityEngine.Random.value, Mathf.Sin(angle)) * spread,
                Start = Time.time,
                Duration = UnityEngine.Random.Range(.6f, .95f)
            });
        }
    }

    private void Popup(Vector3 position, string message, bool vip)
    {
        GameObject root = new GameObject("Tip Popup", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        root.transform.position = position;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 90;
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(220, 50);
        rect.localScale = Vector3.one * .011f;
        Text text = CasinoMinigamePresentation.CreateText(rect, "Tip Amount", message, Vector2.zero,
            new Vector2(220, 50), vip ? 30 : 26, vip ? new Color(1f, .84f, .2f) : new Color(.55f, 1f, .55f));
        text.raycastTarget = false;
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = CasinoHudStyle.Ink;
        outline.effectDistance = new Vector2(2, -2);
        effects.Add(new Fx
        {
            Transform = root.transform,
            Group = root.GetComponent<CanvasGroup>(),
            Velocity = Vector3.up * .45f,
            Start = Time.time,
            Duration = 1.5f
        });
    }

    private SpriteRenderer CreateBillboard(string objectName, Sprite sprite, Vector3 position, float worldSize, int order)
    {
        GameObject prop = new GameObject(objectName);
        prop.transform.SetParent(transform, false);
        prop.transform.position = position;
        SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        float size = sprite != null ? Mathf.Max(.001f, sprite.bounds.size.x) : 1f;
        prop.transform.localScale = Vector3.one * (worldSize / size);
        return renderer;
    }

    private void Update()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        Quaternion facing = targetCamera != null ? targetCamera.transform.rotation : Quaternion.identity;

        for (int i = 0; i < beats.Count; i++)
        {
            Beat beat = beats[i];
            if (Time.time < beat.Time) continue;
            beats.RemoveAt(i--);
            if (beat.Owned && beat.Owner == null) continue;
            try { beat.Action(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        for (int i = flights.Count - 1; i >= 0; i--)
        {
            Flight flight = flights[i];
            if (flight.Prop == null) { flights.RemoveAt(i); continue; }
            float t = Mathf.Clamp01((Time.time - flight.Start) / flight.Duration);
            float eased = t * t * (3 - 2 * t);
            flight.Prop.position = Vector3.Lerp(flight.From, flight.To, eased) + Vector3.up * (flight.Arc * Mathf.Sin(t * Mathf.PI));
            flight.Prop.rotation = facing;
            if (t >= 1)
            {
                Destroy(flight.Prop.gameObject);
                flights.RemoveAt(i);
            }
        }

        for (int i = effects.Count - 1; i >= 0; i--)
        {
            Fx fx = effects[i];
            if (fx.Transform == null) { effects.RemoveAt(i); continue; }
            float t = Mathf.Clamp01((Time.time - fx.Start) / fx.Duration);
            fx.Transform.position += fx.Velocity * Time.deltaTime;
            fx.Velocity *= fx.Group != null ? 1f : .94f;
            fx.Transform.rotation = facing;
            float alpha = 1 - Mathf.Clamp01((t - .6f) / .4f);
            if (fx.Sprite != null)
            {
                Color color = fx.Sprite.color;
                color.a = alpha;
                fx.Sprite.color = color;
            }
            if (fx.Group != null) fx.Group.alpha = alpha;
            if (t >= 1)
            {
                Destroy(fx.Transform.gameObject);
                effects.RemoveAt(i);
            }
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public static void ResetForShift()
    {
        TipsPerformed = 0;
    }
}
