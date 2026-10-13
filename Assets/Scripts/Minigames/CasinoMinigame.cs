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
        presentation = CasinoMinigamePresentation.Open(PresentationTitle, PresentationInstructions);
        OnBegin();
    }

    public void Abandon()
    {
        if (!IsRunning)
        {
            return;
        }

        OnAbandon();
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
        OnComplete(success);
        ClosePresentation();
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
        OnTick(Time.deltaTime);
    }
}
