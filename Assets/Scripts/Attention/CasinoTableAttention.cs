using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CasinoTableAttention : MonoBehaviour
{
    private readonly Dictionary<TableAttentionReason, int> pendingTasks = new Dictionary<TableAttentionReason, int>();

    public event Action<bool> AttentionChanged;
    public bool HasPendingTasks { get; private set; }

    public int GetPendingCount(TableAttentionReason reason)
    {
        return pendingTasks.TryGetValue(reason, out int count) ? count : 0;
    }

    public void RequestAttention(TableAttentionReason reason)
    {
        bool wasNeeded = HasPendingTasks;
        pendingTasks[reason] = GetPendingCount(reason) + 1;
        HasPendingTasks = true;
        if (!wasNeeded)
        {
            AttentionChanged?.Invoke(true);
        }
    }

    public bool ResolveAttention(TableAttentionReason reason)
    {
        int count = GetPendingCount(reason);
        if (count == 0)
        {
            return false;
        }

        if (count == 1)
        {
            pendingTasks.Remove(reason);
        }
        else
        {
            pendingTasks[reason] = count - 1;
        }

        bool stillNeeded = pendingTasks.Count > 0;
        if (HasPendingTasks != stillNeeded)
        {
            HasPendingTasks = stillNeeded;
            AttentionChanged?.Invoke(stillNeeded);
        }

        return true;
    }
}
