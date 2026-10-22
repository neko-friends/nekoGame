using UnityEngine;

public static class MatchResultData
{
    public static bool HasResult { get; private set; }
    public static TeamSide WinnerTeam { get; private set; }
    public static int WinnerPlayerNumber { get; private set; }
    public static string WinnerCharacterId { get; private set; }

    public static void SetResult(
        TeamSide winnerTeam,
        int winnerPlayerNumber,
        string winnerCharacterId)
    {
        // Ÿ”s‚ªŠm’è‚µ‚½Œã‚Ìã‘‚«‚ğ–h‚®B
        if (HasResult)
            return;

        WinnerTeam = winnerTeam;
        WinnerPlayerNumber = winnerPlayerNumber;
        WinnerCharacterId = winnerCharacterId ?? "";
        HasResult = true;
    }

    public static void Reset()
    {
        HasResult = false;
        WinnerPlayerNumber = 0;
        WinnerCharacterId = "";
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnLoad()
    {
        Reset();
    }
}