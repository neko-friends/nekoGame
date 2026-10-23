using System;
using UnityEngine;

/// <summary>左右のキル数だけを管理する。勝敗判定は行わない。</summary>
public class KillCountManager : MonoBehaviour
{
    public event Action<TeamSide, int> KillCountChanged;
    public int TotalKills => leftKills + rightKills;
    public bool IsLocked => isLocked;

    private int leftKills;
    private int rightKills;
    private bool isLocked;

    public int GetKills(TeamSide side) => side == TeamSide.Left ? leftKills : rightKills;

    public void AddKillFor(TeamSide killerSide)
    {
        if (isLocked) return;
        if (killerSide == TeamSide.Left) leftKills++;
        else rightKills++;
        KillCountChanged?.Invoke(killerSide, GetKills(killerSide));
    }

    public void ResetCounts()
    {
        leftKills = 0;
        rightKills = 0;
        isLocked = false;
        KillCountChanged?.Invoke(TeamSide.Left, leftKills);
        KillCountChanged?.Invoke(TeamSide.Right, rightKills);
    }

    public void Lock() => isLocked = true;
}
