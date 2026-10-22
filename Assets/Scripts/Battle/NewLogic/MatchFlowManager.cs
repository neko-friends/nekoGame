using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// PlayerController に実装してもらう口。
/// 識別画面で決まったコントローラーを、試合開始時に渡す。
/// 実装していなければ何もしない(今の入力方式のまま動く)。
/// </summary>
public interface IMatchInputBinding
{
    void BindInput(InputDevice device, string scheme);
}

public class MatchFlowManager : MonoBehaviour
{
    [Header("プレイヤー")]
    [SerializeField] private PlayerController leftPlayer;
    [SerializeField] private PlayerController rightPlayer;

    [Tooltip("左右それぞれのプレイヤーに対応するGameSessionの席番号(1P=左=白 / 2P=右=黒)")]
    [SerializeField, Min(1)] private int leftPlayerNumber = 1;
    [SerializeField, Min(1)] private int rightPlayerNumber = 2;

    [Header("管理スクリプト")]
    [SerializeField] private TeamProgressManager progressManager;
    [SerializeField] private RespawnPointManager respawnPointManager;
    [SerializeField] private MatchTimer matchTimer;

    [Header("復活設定")]
    [SerializeField, Min(0f)] private float respawnDelaySeconds = 1f;

    [Header("タイムアップ時の勝敗(企画書に規定なし。仮)")]
    [Tooltip("タイムアップ時に進行度が同点だった場合の勝者")]
    [SerializeField] private TeamSide tieBreakWinner = TeamSide.Left;

    [Header("勝敗・シーン遷移")]
    [SerializeField] private string resultSceneName = "Result";
    [SerializeField, Min(0f)] private float sceneChangeDelaySeconds = 0f;

    private bool matchEnded;
    private bool hasStarted;
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

    private void Start()
    {
        // 各プレイヤーの Awake が済んでから、識別画面で選んだコントローラーを渡す
        hasStarted = true;
        BindSeatInput(leftPlayer, leftPlayerNumber);
        BindSeatInput(rightPlayer, rightPlayerNumber);
    }

    private void OnDestroy()
    {
        UnsubscribePlayer(leftPlayer);
        UnsubscribePlayer(rightPlayer);
    }

    /// <summary>
    /// タイマー終了時に進行度を比較して勝者を決定する。
    /// 同点の場合は tieBreakWinner(初期値は左陣営)の勝利。
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

        TeamSide winnerSide;

        if (leftScore > rightScore)
            winnerSide = TeamSide.Left;
        else if (rightScore > leftScore)
            winnerSide = TeamSide.Right;
        else
            winnerSide = tieBreakWinner;

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

        // 試合開始後に生成されたキャラは、ここで席のコントローラーを渡す
        if (hasStarted)
            BindSeatInput(player, playerNumber);
    }

    private void UnsubscribePlayer(PlayerController player)
    {
        if (player != null)
            player.Died -= OnPlayerDied;
    }

    // 席(GameSession)のコントローラーをプレイヤーへ渡す
    private void BindSeatInput(PlayerController player, int playerNumber)
    {
        if (player == null)
            return;

        IMatchInputBinding binding = player.GetComponent<IMatchInputBinding>();
        if (binding == null)
            return;

        PlayerSessionData seat = GetSeat(playerNumber);
        if (seat == null || seat.device == null)
        {
            Debug.LogWarning(
                $"MatchFlowManager: P{playerNumber} の席にコントローラー情報がありません。" +
                "識別画面を経由せずに試合シーンを直接再生している可能性があります。",
                this
            );
            return;
        }

        binding.BindInput(seat.device, seat.schemeName);
    }

    private static PlayerSessionData GetSeat(int playerNumber)
    {
        if (GameSession.Seats == null ||
            playerNumber < 1 ||
            playerNumber > GameSession.Seats.Length)
        {
            return null;
        }

        return GameSession.Seats[playerNumber - 1];
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
    /// 陣地移動(暗転後に隣の陣地へ入った)ときに呼び出す。
    /// fieldIndexは 0〜8(左端=0、真ん中=4、右端=8)。
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
    /// 該当する陣営が敵側の関門を破壊したときに呼び出す。
    /// fieldIndexは関門がある陣地(左陣営が壊す関門=6、右陣営が壊す関門=2)。
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
    /// 城の破壊が成立したときに、勝者の陣営(=城を壊した側)を渡す。
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

        if (matchTimer != null)
            matchTimer.StopTimer();

        int winnerPlayerNumber =
            winnerSide == TeamSide.Left
                ? leftPlayerNumber
                : rightPlayerNumber;

        string winnerCharacterId = "";

        PlayerSessionData winnerSeat = GetSeat(winnerPlayerNumber);
        if (winnerSeat != null)
            winnerCharacterId = winnerSeat.characterID;

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
