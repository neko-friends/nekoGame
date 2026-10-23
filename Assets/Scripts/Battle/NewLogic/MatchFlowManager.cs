using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>仮戦闘シーンの試合進行と勝敗確定をまとめる。</summary>
public class MatchFlowManager : MonoBehaviour
{
    [Header("プレイヤー")]
    [SerializeField] private PlayerController leftPlayer;
    [SerializeField] private PlayerController rightPlayer;
    [Tooltip("GameSessionの席番号。左=1P、右=2P")]
    [SerializeField, Min(1)] private int leftPlayerNumber = 1;
    [SerializeField, Min(1)] private int rightPlayerNumber = 2;

    [Header("管理スクリプト")]
    [SerializeField] private TeamProgressManager progressManager;
    [SerializeField] private RespawnPointManager respawnPointManager;
    [SerializeField] private KillCountManager killCountManager;
    [SerializeField] private PlayerDeathRespawnManager deathRespawnManager;
    [SerializeField] private GameTimer gameTimer;

    [Header("試合開始")]
    [SerializeField] private bool placePlayersAtStart = true;

    [Header("タイムアップ時の同点判定")]
    [SerializeField] private TeamSide tieBreakWinner = TeamSide.Left;

    [Header("勝敗・シーン遷移")]
    [SerializeField] private string resultSceneName = "Result";
    [SerializeField, Min(0f)] private float sceneChangeDelaySeconds = 0f;

    public bool HasAnyDeathOccurred { get; private set; }
    public bool MatchEnded => matchEnded;

    private bool matchEnded;
    private bool leftAlive = true;
    private bool rightAlive = true;
    private bool hasPendingCastleResult;
    private TeamSide pendingCastleWinner;
    private bool timeExpiredPending;
    private Coroutine resolveRoutine;

    private void Awake()
    {
        MatchResultData.Reset();
        if (progressManager != null) progressManager.ResetProgress();
        if (killCountManager != null) killCountManager.ResetCounts();
        RegisterPlayer(TeamSide.Left, leftPlayer, leftPlayerNumber);
        RegisterPlayer(TeamSide.Right, rightPlayer, rightPlayerNumber);
        if (deathRespawnManager != null)
        {
            deathRespawnManager.Respawned -= OnPlayerRespawned;
            deathRespawnManager.Respawned += OnPlayerRespawned;
        }
    }

    private void Start()
    {
        if (!placePlayersAtStart || respawnPointManager == null) return;
        PlaceAtStart(TeamSide.Left, leftPlayer);
        PlaceAtStart(TeamSide.Right, rightPlayer);
    }

    private void OnDestroy()
    {
        if (leftPlayer != null) leftPlayer.Died -= OnPlayerDied;
        if (rightPlayer != null) rightPlayer.Died -= OnPlayerDied;
        if (deathRespawnManager != null) deathRespawnManager.Respawned -= OnPlayerRespawned;
    }

    private void PlaceAtStart(TeamSide side, PlayerController player)
    {
        if (player == null) return;
        if (respawnPointManager.TryGetStartPosition(side, out Vector2 position))
        {
            player.transform.position = position;
            if (player.Body != null)
            {
                player.Body.position = position;
                player.Body.linearVelocity = Vector2.zero;
            }
        }
    }

    public void RegisterPlayer(TeamSide side, PlayerController player, int playerNumber)
    {
        if (player == null) return;
        if (side == TeamSide.Left)
        {
            if (leftPlayer != null && leftPlayer != player) leftPlayer.Died -= OnPlayerDied;
            leftPlayer = player;
            leftPlayerNumber = playerNumber;
            leftAlive = true;
        }
        else
        {
            if (rightPlayer != null && rightPlayer != player) rightPlayer.Died -= OnPlayerDied;
            rightPlayer = player;
            rightPlayerNumber = playerNumber;
            rightAlive = true;
        }
        player.Died -= OnPlayerDied;
        player.Died += OnPlayerDied;
        if (deathRespawnManager != null) deathRespawnManager.RegisterPlayer(side, player);
    }

    public PlayerController GetPlayer(TeamSide side) => side == TeamSide.Left ? leftPlayer : rightPlayer;
    public bool IsAlive(TeamSide side) => side == TeamSide.Left ? leftAlive : rightAlive;

    private void OnPlayerDied(PlayerController player)
    {
        if (matchEnded || player == null) return;
        TeamSide deadSide;
        if (player == leftPlayer) deadSide = TeamSide.Left;
        else if (player == rightPlayer) deadSide = TeamSide.Right;
        else
        {
            Debug.LogWarning("MatchFlowManager：登録されていないプレイヤーから死亡通知を受け取りました。", this);
            return;
        }
        if (!IsAlive(deadSide)) return;

        SetAlive(deadSide, false);
        HasAnyDeathOccurred = true;
        TeamSide killerSide = Opposite(deadSide);
        if (killCountManager != null) killCountManager.AddKillFor(killerSide);
        if (deathRespawnManager != null) deathRespawnManager.BeginDeathSequence(deadSide, player);
        else Debug.LogError("MatchFlowManager：PlayerDeathRespawnManagerが未設定です。", this);
    }

    private void OnPlayerRespawned(TeamSide side)
    {
        if (matchEnded) return;
        SetAlive(side, true);
    }

    private void SetAlive(TeamSide side, bool value)
    {
        if (side == TeamSide.Left) leftAlive = value;
        else rightAlive = value;
    }

    public void NotifyTimeExpired()
    {
        if (matchEnded) return;
        timeExpiredPending = true;
        ScheduleResolution();
    }

    public void NotifyCastleDestroyed(TeamSide winnerSide)
    {
        if (matchEnded) return;
        if (!hasPendingCastleResult)
        {
            hasPendingCastleResult = true;
            pendingCastleWinner = winnerSide;
        }
        ScheduleResolution();
    }

    private void ScheduleResolution()
    {
        if (resolveRoutine == null) resolveRoutine = StartCoroutine(ResolveAtEndOfFrame());
    }

    private IEnumerator ResolveAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        resolveRoutine = null;
        if (matchEnded) yield break;

        if (hasPendingCastleResult)
        {
            FinishMatch(pendingCastleWinner);
            yield break;
        }
        if (timeExpiredPending)
        {
            int leftKills = killCountManager != null ? killCountManager.GetKills(TeamSide.Left) : 0;
            int rightKills = killCountManager != null ? killCountManager.GetKills(TeamSide.Right) : 0;
            TeamSide winner = leftKills > rightKills ? TeamSide.Left
                : rightKills > leftKills ? TeamSide.Right
                : tieBreakWinner;
            Debug.Log($"タイムアップ！ キル数：左={leftKills} / 右={rightKills}。勝者={winner}", this);
            FinishMatch(winner);
        }
    }

    private void FinishMatch(TeamSide winnerSide)
    {
        if (matchEnded) return;
        matchEnded = true;
        if (gameTimer != null) gameTimer.StopTimer();
        if (deathRespawnManager != null) deathRespawnManager.CancelAll();
        if (killCountManager != null) killCountManager.Lock();

        int winnerPlayerNumber = winnerSide == TeamSide.Left ? leftPlayerNumber : rightPlayerNumber;
        string winnerCharacterId = string.Empty;
        PlayerSessionData winnerSeat = GetSeat(winnerPlayerNumber);
        if (winnerSeat != null && !string.IsNullOrEmpty(winnerSeat.characterID))
            winnerCharacterId = winnerSeat.characterID;
        else if (GetPlayer(winnerSide) != null)
            winnerCharacterId = GetPlayer(winnerSide).CharacterId;

        MatchResultData.SetResult(winnerSide, winnerPlayerNumber, winnerCharacterId);
        StartCoroutine(LoadResultScene());
    }

    private IEnumerator LoadResultScene()
    {
        if (sceneChangeDelaySeconds > 0f) yield return new WaitForSeconds(sceneChangeDelaySeconds);
        if (string.IsNullOrWhiteSpace(resultSceneName))
        {
            Debug.LogError("MatchFlowManager：Resultシーン名が設定されていません。", this);
            yield break;
        }
        SceneManager.LoadScene(resultSceneName);
    }

    private static PlayerSessionData GetSeat(int playerNumber)
    {
        if (GameSession.Seats == null || playerNumber < 1 || playerNumber > GameSession.Seats.Length) return null;
        return GameSession.Seats[playerNumber - 1];
    }

    private static TeamSide Opposite(TeamSide side) => side == TeamSide.Left ? TeamSide.Right : TeamSide.Left;

    // 旧9陣地側から呼ばれてもコンパイル・動作を壊さない互換口。
    public void NotifyFieldChanged(TeamSide side, int fieldIndex)
    {
        if (matchEnded || progressManager == null) return;
        progressManager.NotifyFieldChanged(side, fieldIndex);
    }

    public void NotifyGateBroken(TeamSide side, int fieldIndex)
    {
        if (matchEnded || progressManager == null) return;
        progressManager.NotifyGateBroken(side, fieldIndex);
    }
}
