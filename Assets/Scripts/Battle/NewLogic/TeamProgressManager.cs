using System;
using UnityEngine;

public enum TeamProgressStage
{
    InitialCenter,       // 試合開始時：中央
    HomeEdgeAfterDeath,  // 初デス後：中央陣地の自陣側端
    GateBefore,          // 陣地移動後：関門前(関門突破前)
    CastleFront          // 関門突破後：城前
}

[Serializable]
public class TeamProgressRecord
{
    // 陣地番号は 0〜8(左端=0、真ん中=4、右端=8)。MatchLayout を参照。
    [SerializeField] public int currentFieldIndex = MatchLayout.StartFieldIndex;
    [SerializeField]
    public TeamProgressStage stage =
        TeamProgressStage.InitialCenter;
    [SerializeField] private bool hasDiedOnce;

    public int CurrentFieldIndex => currentFieldIndex;
    public TeamProgressStage Stage => stage;
    public bool HasDiedOnce => hasDiedOnce;

    public void ResetProgress()
    {
        currentFieldIndex = MatchLayout.StartFieldIndex;
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
        currentFieldIndex = MatchLayout.ClampField(fieldIndex);

        // 関門を突破済みなら城前のまま。陣地が9つになったので、
        // 突破後に次の陣地へ進んだだけで「関門前」へ戻らないようにする。
        if (stage != TeamProgressStage.CastleFront)
            stage = TeamProgressStage.GateBefore;
    }

    public void BreakGate(int fieldIndex)
    {
        currentFieldIndex = MatchLayout.ClampField(fieldIndex);
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

    /// <summary>陣地移動(暗転後に隣の陣地へ入った)ときに呼ぶ。newFieldIndexは0〜8。</summary>
    public void NotifyFieldChanged(TeamSide side, int newFieldIndex)
    {
        if (!MatchLayout.IsValidField(newFieldIndex))
        {
            Debug.LogWarning(
                $"TeamProgressManager: 陣地番号が範囲外です({newFieldIndex})。" +
                $"0〜{MatchLayout.FieldCount - 1}に丸めます。", this);
        }

        GetRecord(side).ChangeField(newFieldIndex);
        NotifyChanged(side);
    }

    /// <summary>
    /// 敵側の関門を破壊したときに呼ぶ。fieldIndexは関門がある陣地
    /// (左陣営が壊す関門=6、右陣営が壊す関門=2)。
    /// </summary>
    public void NotifyGateBroken(TeamSide side, int fieldIndex)
    {
        int expected = MatchLayout.EnemyGateField(side);
        if (fieldIndex != expected)
        {
            Debug.LogWarning(
                $"TeamProgressManager: {side}が壊す関門は陣地{expected}のはずですが、" +
                $"陣地{fieldIndex}で通知されました。", this);
        }

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

    /// <summary>
    /// 陣営の進行度を比較するための点数を返す。
    /// 左陣営は右へ、右陣営は左へ進むほど点数が高くなる。
    /// </summary>
    public int GetProgressScore(TeamSide side)
    {
        TeamProgressRecord record = GetRecord(side);

        int fieldProgress =
            MatchLayout.ForwardProgress(side, record.currentFieldIndex);

        // 陣地の進行を優先し、関門を突破済みなら加点。
        int stageBonus =
            record.stage == TeamProgressStage.CastleFront ? 1 : 0;

        return fieldProgress * 10 + stageBonus;
    }
}
