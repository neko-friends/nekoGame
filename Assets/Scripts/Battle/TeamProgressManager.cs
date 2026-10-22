using System;
using UnityEngine;

public enum TeamProgressStage
{
    InitialCenter,       // 試合開始時：中央
    HomeEdgeAfterDeath,  // 初デス後：中央フィールドの自陣側端
    GateBefore,           // フィールド切り替え後：関門前
    CastleFront           // 関門突破後：城前
}

[Serializable]
public class TeamProgressRecord
{
    [SerializeField] private int currentFieldIndex = 1;
    [SerializeField]
    private TeamProgressStage stage =
        TeamProgressStage.InitialCenter;
    [SerializeField] private bool hasDiedOnce;

    public int CurrentFieldIndex => currentFieldIndex;
    public TeamProgressStage Stage => stage;
    public bool HasDiedOnce => hasDiedOnce;

    public void ResetProgress()
    {
        currentFieldIndex = 1; // 0=左、1=中央、2=右
        stage = TeamProgressStage.InitialCenter;
        hasDiedOnce = false;
    }

    public bool RegisterFirstDeath()
    {
        if (hasDiedOnce)
            return false;

        hasDiedOnce = true;

        // すでに先の段階へ進んでいた場合は、復活地点を後退させない。
        if (stage == TeamProgressStage.InitialCenter)
            stage = TeamProgressStage.HomeEdgeAfterDeath;

        return true;
    }

    public void ChangeField(int fieldIndex)
    {
        currentFieldIndex = Mathf.Clamp(fieldIndex, 0, 2);
        stage = TeamProgressStage.GateBefore;
    }

    public void BreakGate(int fieldIndex)
    {
        currentFieldIndex = Mathf.Clamp(fieldIndex, 0, 2);
        stage = TeamProgressStage.CastleFront;
    }
}

public class TeamProgressManager : MonoBehaviour
{
    [Header("左右の陣営の進行状況")]
    [SerializeField]
    private TeamProgressRecord leftProgress =
        new TeamProgressRecord();

    [SerializeField]
    private TeamProgressRecord rightProgress =
        new TeamProgressRecord();

    public event Action<TeamSide, TeamProgressStage, int> ProgressChanged;

    private TeamProgressRecord GetRecord(TeamSide side)
    {
        return side == TeamSide.Left ? leftProgress : rightProgress;
    }

    public TeamProgressStage GetStage(TeamSide side)
    {
        return GetRecord(side).Stage;
    }

    public int GetCurrentFieldIndex(TeamSide side)
    {
        return GetRecord(side).CurrentFieldIndex;
    }

    public void ResetProgress()
    {
        leftProgress.ResetProgress();
        rightProgress.ResetProgress();

        NotifyChanged(TeamSide.Left);
        NotifyChanged(TeamSide.Right);
    }

    public void NotifyFirstDeath(TeamSide side)
    {
        TeamProgressRecord record = GetRecord(side);

        if (record.RegisterFirstDeath())
            NotifyChanged(side);
    }

    public void NotifyFieldChanged(TeamSide side, int newFieldIndex)
    {
        GetRecord(side).ChangeField(newFieldIndex);
        NotifyChanged(side);
    }

    public void NotifyGateBroken(TeamSide side, int fieldIndex)
    {
        GetRecord(side).BreakGate(fieldIndex);
        NotifyChanged(side);
    }

    private void NotifyChanged(TeamSide side)
    {
        TeamProgressRecord record = GetRecord(side);

        ProgressChanged?.Invoke(
            side,
            record.Stage,
            record.CurrentFieldIndex
        );
    }
}