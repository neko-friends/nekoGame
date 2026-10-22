using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

public class LobbyManager : MonoBehaviour
{
    [Header("対戦設定")]
    [SerializeField] private string matchSceneName = "MatchScene";

    [Header("キャラクター一覧")]
    [SerializeField]
    private string[] characterIDs =
    {
        "Character01",
        "Character02",
        "Character03"
    };

    [Header("ロビーUI")]
    [SerializeField] private TMP_Text player1InfoText;
    [SerializeField] private TMP_Text player2InfoText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text modeInfoText;
    [SerializeField] private TMP_Text startInfoText;

    private LobbyPlayer player1;
    private LobbyPlayer player2;

    public int PlayerCount { get; private set; }

    private void Start()
    {
        UpdatePlayerCount();
    }

    // PlayerInputManagerのPlayer Joined Eventから呼び出す
    public void OnPlayerJoined(PlayerInput playerInput)
    {
        if (playerInput == null)
            return;

        LobbyPlayer lobbyPlayer =
            playerInput.GetComponent<LobbyPlayer>();

        if (lobbyPlayer == null)
        {
            Debug.LogError(
                "参加用PrefabにLobbyPlayerがありません。");

            Destroy(playerInput.gameObject);
            return;
        }

        // 参加順に空いているスロットへ割り当てる
        int playerNumber;

        if (player1 == null)
        {
            playerNumber = 1;
            player1 = lobbyPlayer;
        }
        else if (player2 == null)
        {
            playerNumber = 2;
            player2 = lobbyPlayer;
        }
        else
        {
            Debug.Log("参加できるのは最大2人です。");
            Destroy(playerInput.gameObject);
            return;
        }

        lobbyPlayer.Initialize(
            playerNumber,
            this,
            playerInput
        );

        UpdatePlayerCount();
    }

    // PlayerInputManagerのPlayer Left Eventから呼び出す
    public void OnPlayerLeft(PlayerInput playerInput)
    {
        if (playerInput == null)
            return;

        LobbyPlayer leavingPlayer =
            playerInput.GetComponent<LobbyPlayer>();

        if (leavingPlayer == player1)
            player1 = null;

        if (leavingPlayer == player2)
            player2 = null;

        // P1が退出し、P2だけ残った場合はP2をP1に繰り上げる
        if (player1 == null && player2 != null)
        {
            player1 = player2;
            player2 = null;

            player1.Initialize(
                1,
                this,
                player1.PlayerInput
            );
        }

        // 退出後に残ったP2がいれば、P2として再設定する
        if (player1 != null && player2 != null)
        {
            player2.Initialize(
                2,
                this,
                player2.PlayerInput
            );
        }

        UpdatePlayerCount();
    }

    private void UpdatePlayerCount()
    {
        PlayerCount = 0;

        if (player1 != null)
            PlayerCount++;

        if (player2 != null)
            PlayerCount++;

        GameMode previousMode = GameSession.CurrentMode;

        // 人数に応じてモードを自動変更
        if (PlayerCount == 1)
        {
            GameSession.CurrentMode = GameMode.Solo;
        }
        else
        {
            // 0人または2人の場合は初期モードに戻す
            GameSession.CurrentMode = GameMode.Versus;
        }

        // モードが変わったら準備状態をリセット
        if (previousMode != GameSession.CurrentMode)
        {
            if (player1 != null)
                player1.SetReady(false);

            if (player2 != null)
                player2.SetReady(false);
        }

        RefreshLobbyUI();
    }

    public string GetNextCharacter(string currentID)
    {
        return GetCharacterByOffset(currentID, 1);
    }

    public string GetPreviousCharacter(string currentID)
    {
        return GetCharacterByOffset(currentID, -1);
    }

    private string GetCharacterByOffset(
        string currentID,
        int offset)
    {
        if (characterIDs == null || characterIDs.Length == 0)
            return currentID;

        int index = System.Array.IndexOf(
            characterIDs, currentID);

        if (index < 0)
            index = 0;

        int nextIndex =
            (index + offset + characterIDs.Length)
            % characterIDs.Length;

        return characterIDs[nextIndex];
    }

    // P1が開始操作をしたときに呼び出す
    public void RequestStart()
    {
        if (player1 == null || !player1.IsReady)
        {
            Debug.Log("P1の準備完了が必要です。");
            return;
        }

        if (GameSession.CurrentMode == GameMode.Versus)
        {
            if (player2 == null || !player2.IsReady)
            {
                Debug.Log("P2の準備完了が必要です。");
                return;
            }
        }

        GameSession.Player1 =
            player1.CreateSessionData(PlayerControlType.Human);

        if (GameSession.CurrentMode == GameMode.Solo)
        {
            GameSession.Player2 = new PlayerSessionData
            {
                playerNumber = 2,
                controlType = PlayerControlType.CPU,
                characterID = "Character02"
            };
        }
        else
        {
            GameSession.Player2 =
                player2.CreateSessionData(PlayerControlType.Human);
        }

        SceneManager.LoadScene(matchSceneName);
    }

    public void RefreshLobbyUI()
    {
        if (player1InfoText != null)
        {
            player1InfoText.text = player1 == null
                ? "P1：未参加"
                : $"P1：{player1.SelectedCharacterID}\n" +
                  (player1.IsReady ? "準備完了" : "選択中");
        }

        if (player2InfoText != null)
        {
            player2InfoText.text =
                GameSession.CurrentMode == GameMode.Solo
                ? "P2：CPU"
                : player2 == null
                    ? "P2：未参加"
                    : $"P2：{player2.SelectedCharacterID}\n" +
                      (player2.IsReady ? "準備完了" : "選択中");
        }

        if (playerCountText != null)
        {
            playerCountText.text =
                $"参加人数：{PlayerCount}人";
        }

        if (modeInfoText != null)
        {
            modeInfoText.text =
                $"モード：{GameSession.CurrentMode}";
        }

        if (startInfoText != null)
        {
            bool canStart =
                player1 != null &&
                player1.IsReady &&
                (GameSession.CurrentMode == GameMode.Solo ||
                 (player2 != null && player2.IsReady));

            startInfoText.text = canStart
                ? "開始できます"
                : "プレイヤーの準備を待っています";
        }
    }
}