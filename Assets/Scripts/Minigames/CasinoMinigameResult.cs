public readonly struct CasinoMinigameResult
{
    public readonly bool Success;
    public readonly int Score;
    public readonly float CompletionTimeSeconds;

    public CasinoMinigameResult(bool success, int score, float completionTimeSeconds)
    {
        Success = success;
        Score = score;
        CompletionTimeSeconds = completionTimeSeconds;
    }
}
