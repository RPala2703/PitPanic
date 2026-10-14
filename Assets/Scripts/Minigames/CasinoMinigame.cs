using System;
using UnityEngine;

public enum CasinoMinigameType
{
    Flick,
    Stack,
    Pack,
    Sweep,
    Spin,
    Spot,
    Approve,
    Shuffle,
    Rotate
}

[DisallowMultipleComponent]
public abstract class CasinoMinigame : MonoBehaviour
{
    private float elapsedSeconds;
    private CasinoMinigamePresentation presentation;

    public event Action<CasinoMinigameResult> Completed;
    public event Action Abandoned;
    public bool IsRunning { get; private set; }
    protected CasinoMinigamePresentation Presentation => presentation;
    public virtual bool CanBegin => true;
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
        if (presentation != null)
        {
            presentation.Close();
            presentation = null;
        }
    }

    private void Update()
    {
        if (!IsRunning)
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
