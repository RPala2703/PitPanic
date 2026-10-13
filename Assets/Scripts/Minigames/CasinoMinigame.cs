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

    public event Action<CasinoMinigameResult> Completed;
    public event Action Abandoned;
    public bool IsRunning { get; private set; }
    public virtual bool CanBegin => true;

    public void Begin()
    {
        if (IsRunning)
        {
            throw new InvalidOperationException($"{GetType().Name} is already running.");
        }

        elapsedSeconds = 0f;
        IsRunning = true;
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
        Abandoned?.Invoke();
    }

    protected abstract void OnBegin();
    protected abstract void OnTick(float deltaTime);
    protected abstract void OnAbandon();

    protected void Complete(bool success, int score)
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        Completed?.Invoke(new CasinoMinigameResult(success, Mathf.Max(0, score), elapsedSeconds));
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
