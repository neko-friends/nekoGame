using UnityEngine;

/// <summary>
/// 企画書v1.0.1「バトル基本システム」「ゲーム画面」で決まっている陣地の配置ルール。
/// 陣地番号は 左端=0 〜 右端=8。左陣営は右へ(番号が増える方向)、右陣営は左へ進む。
///   陣地数      : 9
///   開始位置    : 真ん中の陣地(4)
///   関門        : 陣地の一番端から2つ進んだ陣地の城側の端 → 左の関門=陣地2 / 右の関門=陣地6
///   城          : 一番端の陣地(0 / 8)の奥
/// </summary>
public static class MatchLayout
{
    public const int FieldCount = 9;
    public const int StartFieldIndex = 4;
    public const int GateOffsetFromEnd = 2;

    public static int LeftGateField => GateOffsetFromEnd;
    public static int RightGateField => FieldCount - 1 - GateOffsetFromEnd;

    public static TeamSide Opposite(TeamSide side)
    {
        return side == TeamSide.Left ? TeamSide.Right : TeamSide.Left;
    }

    /// <summary>attacker が破壊すべき(敵側の)関門がある陣地。</summary>
    public static int EnemyGateField(TeamSide attacker)
    {
        return attacker == TeamSide.Left ? RightGateField : LeftGateField;
    }

    /// <summary>attacker が破壊すべき(敵側の)城がある陣地。</summary>
    public static int EnemyCastleField(TeamSide attacker)
    {
        return attacker == TeamSide.Left ? FieldCount - 1 : 0;
    }

    public static bool IsValidField(int fieldIndex)
    {
        return fieldIndex >= 0 && fieldIndex < FieldCount;
    }

    public static int ClampField(int fieldIndex)
    {
        return Mathf.Clamp(fieldIndex, 0, FieldCount - 1);
    }

    /// <summary>前進度(0〜8)。左陣営は番号そのまま、右陣営は反転。</summary>
    public static int ForwardProgress(TeamSide side, int fieldIndex)
    {
        int clamped = ClampField(fieldIndex);
        return side == TeamSide.Left ? clamped : FieldCount - 1 - clamped;
    }
}
