using NUnit.Framework.Interfaces;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MatchFlowManager : MonoBehaviour
{
    [Header("プレイヤー")]
    [SerializeField] private PlayerController leftPlayer;
    [SerializeField] private PlayerController rightPlayer;

    [Tooltip("左右それぞれのプレイヤーに対応するGameSessionの席番号")]
    [SerializeField, Min(1)] private int leftPlayerNumber = 1;
    [SerializeField, Min(1)] private int rightPlayerNumber = 2;

    [Header("管理スクリプト")]
    [SerializeField] private TeamProgressManager progressManager;
    [SerializeField] private RespawnPointManager respawnPointManager;

    [Header("復活設定")]
    [SerializeField, Min(0f)] private float respawnDelaySeconds = 1f;

    [Header("勝敗・シーン遷移")]
    [SerializeField] private string resultSceneName = "Result";
    [SerializeField, Min(0f)] private float sceneChangeDelaySeconds = 0f;

    private bool matchEnded;
    private bool leftRespawnPending;
    private bool rightRespawnPending;

    private void Awake()
    {
        MatchResultData.Reset();

        if (progressManager != null)
            progressManager.ResetProgress();

        RegisterPlayer(TeamSide.Left, leftPlayer, leftPlayerNumber);
        RegisterPlayer(TeamSide.Right, rightPlayer, rightPlayerNumber);
    }

    private void OnDestroy()
    {
        UnsubscribePlayer(leftPlayer);
        UnsubscribePlayer(rightPlayer);
    }

    /// <summary>
    /// タイマー終了時に進行度を比較して勝者を決定する。
    /// 同点の場合は左陣営の勝利。
    /// </summary>
    public void NotifyTimeExpired()
    {
        if (matchEnded)
        {
            return;
        }

        if (progressManager == null)
        {
            Debug.LogError(
                "MatchFlowManager: TeamProgressManagerが設定されていません。"
            );
            return;
        }

        int leftScore =
            progressManager.GetProgressScore(TeamSide.Left);

        int rightScore =
            progressManager.GetProgressScore(TeamSide.Right);

        Debug.Log(
            $"タイムアップ！ 左陣営={leftScore} / 右陣営={rightScore}"
        );

        // 同点なら左陣営が勝利。
        TeamSide winnerSide =
            leftScore >= rightScore
                ? TeamSide.Left
                : TeamSide.Right;

        FinishMatch(winnerSide);
    }

    /// <summary>
    /// プレイヤーを登録する。
    /// 動的にキャラクターを生成する場合も、生成後に呼び出せる。
    /// </summary>
    public void RegisterPlayer(
        TeamSide side,
        PlayerController player,
        int playerNumber)
    {
        if (player == null)
            return;

        if (side == TeamSide.Left)
        {
            if (leftPlayer != player)
                UnsubscribePlayer(leftPlayer);

            leftPlayer = player;
            leftPlayerNumber = playerNumber;
        }
        else
        {
            if (rightPlayer != player)
                UnsubscribePlayer(rightPlayer);

            rightPlayer = player;
            rightPlayerNumber = playerNumber;
        }

        player.Died -= OnPlayerDied;
        player.Died += OnPlayerDied;
    }

    private void UnsubscribePlayer(PlayerController player)
    {
        if (player != null)
            player.Died -= OnPlayerDied;
    }

    private void OnPlayerDied(PlayerController player)
    {
        if (matchEnded || player == null)
            return;

        TeamSide side;

        if (player == leftPlayer)
        {
            side = TeamSide.Left;

            if (leftRespawnPending)
                return;

            leftRespawnPending = true;
        }
        else if (player == rightPlayer)
        {
            side = TeamSide.Right;

            if (rightRespawnPending)
                return;

            rightRespawnPending = true;
        }
        else
        {
            Debug.LogWarning(
                "未登録のプレイヤーから死亡通知を受け取りました。"
            );
            return;
        }

        // 初デスの進行段階を更新する。
        if (progressManager != null)
            progressManager.NotifyFirstDeath(side);

        StartCoroutine(RespawnAfterDelay(side, player));
    }

    private IEnumerator RespawnAfterDelay(
        TeamSide side,
        PlayerController player)
    {
        yield return new WaitForSeconds(respawnDelaySeconds);

        if (side == TeamSide.Left)
            leftRespawnPending = false;
        else
            rightRespawnPending = false;

        if (matchEnded || player == null)
            yield break;

        if (respawnPointManager == null)
        {
            Debug.LogError(
                "RespawnPointManagerが未設定です。"
            );
            yield break;
        }

        if (respawnPointManager.TryGetSpawnPosition(
            side, out Vector2 spawnPosition))
        {
            player.Revive(spawnPosition);
        }
    }

    /// <summary>
    /// フィールド切り替えが成立したときに呼び出す。
    /// fieldIndexは0=左、1=中央、2=右。
    /// </summary>
    public void NotifyFieldChanged(
        TeamSide side,
        int fieldIndex)
    {
        if (matchEnded || progressManager == null)
            return;

        progressManager.NotifyFieldChanged(side, fieldIndex);
    }

    /// <summary>
    /// 該当する陣営が関門を突破したときに呼び出す。
    /// </summary>
    public void NotifyGateBroken(
        TeamSide side,
        int fieldIndex)
    {
        if (matchEnded || progressManager == null)
            return;

        progressManager.NotifyGateBroken(side, fieldIndex);
    }

    /// <summary>
    /// 城の破壊が成立したときに、勝者の陣営を渡す。
    /// </summary>
    public void NotifyCastleDestroyed(TeamSide winnerSide)
    {
        FinishMatch(winnerSide);
    }

    private void FinishMatch(TeamSide winnerSide)
    {
        if (matchEnded)
        {
            return;
        }

        matchEnded = true;

        int winnerPlayerNumber =
            winnerSide == TeamSide.Left
                ? leftPlayerNumber
                : rightPlayerNumber;

        string winnerCharacterId = "";

        if (GameSession.Seats != null &&
            winnerPlayerNumber > 0 &&
            winnerPlayerNumber <= GameSession.Seats.Length)
        {
            winnerCharacterId =
                GameSession.Seats[winnerPlayerNumber - 1].characterID;
        }

        MatchResultData.SetResult(
            winnerSide,
            winnerPlayerNumber,
            winnerCharacterId
        );

        StartCoroutine(LoadResultScene());
    }

    private IEnumerator LoadResultScene()
    {
        yield return new WaitForSeconds(sceneChangeDelaySeconds);

        if (string.IsNullOrWhiteSpace(resultSceneName))
        {
            Debug.LogError("Resultシーン名が設定されていません。");
            yield break;
        }

        SceneManager.LoadScene(resultSceneName);
    }
}