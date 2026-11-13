using System;
using UnityEngine;

public enum CasinoMinigameType
{
    LegacyFlick,
    Stack,
    Pack,
    Sweep,
    Spin,
    Spot,
    Approve,
    Shuffle,
    Rotate,
    Cover
}

[DisallowMultipleComponent]
public abstract class CasinoMinigame : MonoBehaviour
{
    private float elapsedSeconds;
    private CasinoMinigamePresentation presentation;
    private bool hasCarriedDeadline;
    private float carriedRemaining;
    private float carriedDuration;
    private int carriedAttemptKey;

    public event Action<CasinoMinigameResult> Completed;
    public event Action Abandoned;
    public bool IsRunning { get; private set; }
    protected CasinoMinigamePresentation Presentation => presentation;
    public virtual bool CanBegin => true;
    public virtual bool TryGetDeadline(out float remaining, out float duration)
    {
        remaining = duration = 0;
        return false;
    }
    // Tasks that opt in keep one countdown across failed or exited attempts.
    protected virtual bool CarriesDeadlineBetweenAttempts => false;
    protected virtual int AttemptKey => 0;
    public bool HasCarriedDeadline => hasCarriedDeadline;

    public bool TryGetCarriedDeadline(out float remaining, out float duration)
    {
        remaining = carriedRemaining;
        duration = carriedDuration;
        return !IsRunning && hasCarriedDeadline && remaining > 0 && duration > 0;
    }

    public void ClearCarriedDeadline() => hasCarriedDeadline = false;

    protected float ConsumeCarriedDeadline(float fullDuration)
    {
        bool carry = hasCarriedDeadline && carriedAttemptKey == AttemptKey && carriedRemaining > 0;
        hasCarriedDeadline = false;
        return carry ? Mathf.Min(carriedRemaining, fullDuration) : fullDuration;
    }

    private void CarryDeadline()
    {
        if (!CarriesDeadlineBetweenAttempts || !TryGetDeadline(out float remaining, out float duration) || remaining <= 0)
            return;
        hasCarriedDeadline = true;
        carriedRemaining = remaining;
        carriedDuration = duration;
        carriedAttemptKey = AttemptKey;
    }
    protected virtual string PresentationTitle => GetType().Name.Replace("Minigame", string.Empty).ToUpperInvariant();
    protected virtual string PresentationInstructions => "Complete the service task. The casino remains live.";

    public void Begin()
    {
        if (IsRunning)
        {
            throw new InvalidOperationException($"{GetType().Name} is already running.");
        }

        if (CasinoMinigamePresentation.IsOpen)
        {
            throw new InvalidOperationException("Another casino minigame is already active.");
        }

        elapsedSeconds = 0f;
        CasinoSoundscape.SetFocus(transform);
        IsRunning = true;
        try
        {
            presentation = CasinoMinigamePresentation.Open(PresentationTitle, PresentationInstructions);
            OnBegin();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
            AbortAfterRuntimeError();
        }
    }

    public void Abandon()
    {
        if (!IsRunning)
        {
            return;
        }

        CarryDeadline();
        try
        {
            OnAbandon();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }

        IsRunning = false;
        ClosePresentation();
        Abandoned?.Invoke();
    }

    protected abstract void OnBegin();
    protected abstract void OnTick(float deltaTime);
    protected abstract void OnAbandon();
    protected virtual void OnComplete(bool success) { }

    protected void Complete(bool success, int score)
    {
        if (!IsRunning)
        {
            return;
        }

        if (success) hasCarriedDeadline = false;
        else CarryDeadline();
        IsRunning = false;
        try
        {
            OnComplete(success);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }
        finally
        {
            ClosePresentation();
        }

        Completed?.Invoke(new CasinoMinigameResult(success, Mathf.Max(0, score), elapsedSeconds));
    }

    private void ClosePresentation()
    {
        CasinoSoundscape.ReleaseFocus(transform);
        if (presentation != null)
        {
            presentation.Close();
            presentation = null;
        }
    }

    private void Update()
    {
        if (!IsRunning && hasCarriedDeadline && Time.timeScale > 0)
        {
            if (carriedAttemptKey != AttemptKey) hasCarriedDeadline = false;
            else
            {
                carriedRemaining -= Time.deltaTime;
                if (carriedRemaining <= 0)
                {
                    // The overall task countdown ran out while the player was away: that is a missed task.
                    hasCarriedDeadline = false;
                    Completed?.Invoke(new CasinoMinigameResult(false, 0, elapsedSeconds));
                }
            }
        }

        if (!IsRunning || Time.timeScale == 0)
        {
            return;
        }

        elapsedSeconds += Time.deltaTime;
        try
        {
            OnTick(Time.deltaTime);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
            AbortAfterRuntimeError();
        }
    }

    private void AbortAfterRuntimeError()
    {
        if (!IsRunning)
        {
            return;
        }

        try
        {
            OnAbandon();
        }
        catch (System.Exception cleanupException)
        {
            Debug.LogException(cleanupException, this);
        }

        IsRunning = false;
        ClosePresentation();
        Abandoned?.Invoke();
    }
}
