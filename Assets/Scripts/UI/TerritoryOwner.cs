
using UnityEngine;

public enum TerritoryOwner
{
    Neutral,  // 未占有
    Left,     // 左プレイヤー
    Right,    // 右プレイヤー
    Mixed     // 試合開始時の中央区画
}

public class TerritoryManager : MonoBehaviour
{
    public const int SectionCount = 7;

    [Header("ステージ範囲")]
    [SerializeField] private float stageLeftX = 0f;
    [SerializeField] private float stageRightX = 172.8f;

    [Header("7区画の状態")]
    [SerializeField]
    private TerritoryOwner[] sections =
        new TerritoryOwner[SectionCount];

    public TerritoryOwner GetSectionOwner(int index)
    {
        if (index < 0 || index >= SectionCount)
            return TerritoryOwner.Neutral;

        return sections[index];
    }

    private void Awake()
    {
        ResetTerritory();
    }

    // 試合開始時の状態に戻す
    public void ResetTerritory()
    {
        for (int i = 0; i < SectionCount; i++)
        {
            sections[i] = TerritoryOwner.Neutral;
        }

        // 中央区画だけ左右の色を混ぜて表示する
        sections[3] = TerritoryOwner.Mixed;
    }

    // 倒した側と、倒した瞬間のワールド座標を受け取る
    public void CaptureTerritory(
        TeamSide winnerSide,
        float defeatedPositionX)
    {
        float width = stageRightX - stageLeftX;

        if (width <= 0f)
            return;

        float normalizedX =
            (defeatedPositionX - stageLeftX) / width;

        int targetIndex = Mathf.Clamp(
            Mathf.FloorToInt(normalizedX * SectionCount),
            0,
            SectionCount - 1
        );

        if (winnerSide == TeamSide.Left)
        {
            // 左端から倒した位置の区画まで左側の色にする
            for (int i = 0; i <= targetIndex; i++)
            {
                sections[i] = TerritoryOwner.Left;
            }
        }
        else
        {
            // 右端から倒した位置の区画まで右側の色にする
            for (int i = SectionCount - 1;
                 i >= targetIndex;
                 i--)
            {
                sections[i] = TerritoryOwner.Right;
            }
        }
    }
}
