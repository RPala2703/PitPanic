using System;
using UnityEngine;

public sealed class CustomerPatience : MonoBehaviour
{
    public event Action PatienceExpired;

    public float Remaining { get; private set; }
    public float Duration { get; private set; }
    public float Normalized => Duration <= 0f ? 0f : Remaining / Duration;
    public bool IsWaiting { get; private set; }

    public void Begin(float patienceSeconds)
    {
        Duration = Mathf.Max(1f, patienceSeconds);
        Remaining = Duration;
        IsWaiting = true;
    }

    public void Pause()
    {
        IsWaiting = false;
    }

    public void Resume()
    {
        if (Remaining > 0f)
        {
            IsWaiting = true;
        }
    }

    private void Update()
    {
        if (!IsWaiting)
        {
            return;
        }

        Remaining = Mathf.Max(0f, Remaining - Time.deltaTime);
        if (Remaining <= 0f)
        {
            IsWaiting = false;
            PatienceExpired?.Invoke();
        }
    }
}
