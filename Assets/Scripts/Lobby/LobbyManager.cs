using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ロビーの進行を管理する。
/// ・参加は PlayerInputManager を使わず、ここで入力を監視して手動で行う
/// ・席の情報(GameSession.Seats)を正として、モードは席から導く
/// ・人数が変わったら準備完了を取り消す
/// ・開始できるのは1Pのスタートボタンだけ(人間の席が全員準備完了のとき)
/// </summary>
public class LobbyManager : MonoBehaviour
{
    [Header("シーン名")]
    [SerializeField] private string matchSceneName = "MatchScene";
    [SerializeField] private string titleSceneName = "TitleScene";

    [Header("キャラクター一覧")]
    [SerializeField]
    private string[] characterIDs =
    {
        "Character01",
        "Character02",
        "Character03"
    };

    [Header("ロビーUI")]
    [SerializeField] private Text player1InfoText;
    [SerializeField] private Text player2InfoText;
    [SerializeField] private Text playerCountText;
    [SerializeField] private Text modeInfoText;
    [SerializeField] private Text startInfoText;
    [SerializeField] private Text noticeText;       // 一言通知用(任意)

    [SerializeField] private float noticeSeconds = 2.5f;

    // 席(添字0 = 1P、添字1 = 2P)。空席は null
    private readonly LobbyPlayer[] seats = new LobbyPlayer[GameSession.SeatCount];

    private System.IDisposable anyButtonSubscription;
    private float noticeTimer;

    public int PlayerCount
    {
        get
        {
            int count = 0;
            foreach (var s in seats) if (s != null) count++;
            return count;
        }
    }

    // トレーニングでは人間は1人だけ(2P席はダミー)
    private int MaxHumans =>
        GameSession.Entry == EntryType.Training ? 1 : GameSession.SeatCount;

    // =================================================================
    // Unity イベント
    // =================================================================

    private void OnEnable()
    {
        // どのコントローラーのボタンが押されても通知を受ける(参加の検知用)
        anyButtonSubscription = InputSystem.onAnyButtonPress.Call(OnAnyButtonPressed);
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        anyButtonSubscription?.Dispose();
        anyButtonSubscription = null;
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void Start()
    {
        RestoreSeatsFromSession();
        JoinPendingFromTitle();
        RefreshLobbyUI();
    }

    private void Update()
    {
        // 参加者それぞれの入力を読む(途中で席が変わっても安全なように添字で回す)
        for (int i = 0; i < seats.Length; i++)
        {
            seats[i]?.Tick();
        }

        if (noticeTimer > 0f)
        {
            noticeTimer -= Time.deltaTime;
            if (noticeTimer <= 0f && noticeText != null) noticeText.text = "";
        }
    }

    // =================================================================
    // 参加の検知
    // =================================================================

    private void OnAnyButtonPressed(InputControl control)
    {
        InputDevice device = control.device;
        string scheme = null;
        bool isConfirm = false;
        bool isCancel = false;

        if (device is Gamepad pad)
        {
            scheme = LobbySchemes.GamepadScheme;
            isConfirm = control == pad.buttonSouth;   // A
            isCancel = control == pad.buttonEast;     // B
        }
        else if (control is KeyControl key)
        {
            var k1 = LobbySchemes.Keyboard1Keys;
            var k2 = LobbySchemes.Keyboard2Keys;

            if (key.keyCode == k1.confirm) { scheme = LobbySchemes.Keyboard1Scheme; isConfirm = true; }
            else if (key.keyCode == k2.confirm) { scheme = LobbySchemes.Keyboard2Scheme; isConfirm = true; }
            else if (key.keyCode == k1.cancel) { scheme = LobbySchemes.Keyboard1Scheme; isCancel = true; }
            else if (key.keyCode == k2.cancel) { scheme = LobbySchemes.Keyboard2Scheme; isCancel = true; }
        }

        if (scheme == null) return;

        if (isConfirm)
        {
            TryJoin(device, scheme);
        }
        else if (isCancel && PlayerCount == 0)
        {
            // 誰も参加していない状態でキャンセル → タイトルへ
            ReturnToTitle();
        }
    }

    // =================================================================
    // 席の管理
    // =================================================================

    private bool TryJoin(InputDevice device, string scheme)
    {
        if (PlayerCount >= MaxHumans) return false;

        // 同じコントローラー(同じスキーム)の二重参加は不可
        foreach (var s in seats)
        {
            if (s != null && s.Device == device && s.SchemeName == scheme) return false;
        }

        int index = FirstEmptySeat();
        if (index < 0) return false;

        seats[index] = new LobbyPlayer(this, device, scheme, index + 1, DefaultCharacterFor(index));

        OnSeatsChanged();
        return true;
    }

    public void RequestLeave(LobbyPlayer player)
    {
        for (int i = 0; i < seats.Length; i++)
        {
            if (seats[i] == player) seats[i] = null;
        }

        CompactSeats();
        OnSeatsChanged();
    }

    // 1Pが抜けたら2Pを1Pへ繰り上げる(開始の権限を持つ人を常に確保するため)
    private void CompactSeats()
    {
        for (int i = 0; i < seats.Length; i++)
        {
            if (seats[i] != null) continue;

            for (int j = i + 1; j < seats.Length; j++)
            {
                if (seats[j] == null) continue;

                seats[i] = seats[j];
                seats[j] = null;
                seats[i].SetPlayerNumber(i + 1);
                break;
            }
        }
    }

    // 人数が変わったとき: 全員の準備完了を取り消す(キャラ選択は保持)
    private void OnSeatsChanged()
    {
        bool anyReady = false;

        foreach (var s in seats)
        {
            if (s == null) continue;

            if (s.IsReady) anyReady = true;
            s.SetReady(false);
        }

        if (anyReady)
        {
            ShowNotice("人数が変わったため、準備完了を取り消しました");
        }

        RefreshLobbyUI();
    }

    private int FirstEmptySeat()
    {
        for (int i = 0; i < seats.Length; i++)
        {
            if (seats[i] == null) return i;
        }
        return -1;
    }

    private string DefaultCharacterFor(int seatIndex)
    {
        if (characterIDs == null || characterIDs.Length == 0) return "Character01";
        return characterIDs[seatIndex % characterIDs.Length];
    }

    // コントローラーが抜けたら、その席を空ける
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change != InputDeviceChange.Disconnected && change != InputDeviceChange.Removed) return;

        bool removed = false;

        for (int i = 0; i < seats.Length; i++)
        {
            if (seats[i] != null && seats[i].Device == device)
            {
                seats[i] = null;
                removed = true;
            }
        }

        if (!removed) return;

        CompactSeats();
        ShowNotice("コントローラーが切断されました");
        OnSeatsChanged();
    }

    // =================================================================
    // 復元(再戦・ロビーへ戻る・タイトルからの引き継ぎ)
    // =================================================================

    // セッションに残っている人間の席を、同じコントローラーで復元する(準備完了はリセット)
    private void RestoreSeatsFromSession()
    {
        for (int i = 0; i < GameSession.Seats.Length; i++)
        {
            PlayerSessionData data = GameSession.Seats[i];

            if (data == null || data.controlType != PlayerControlType.Human) continue;
            if (data.device == null || !data.device.added) continue;   // 接続されていなければ復元しない
            if (PlayerCount >= MaxHumans) break;

            seats[i] = new LobbyPlayer(this, data.device, data.schemeName, i + 1, data.characterID);
        }

        CompactSeats();
    }

    // タイトルで決定を押したコントローラーを、1Pとして自動で参加させる
    private void JoinPendingFromTitle()
    {
        if (GameSession.PendingJoinDevice != null && PlayerCount == 0)
        {
            TryJoin(GameSession.PendingJoinDevice, GameSession.PendingJoinScheme);
        }

        GameSession.PendingJoinDevice = null;
        GameSession.PendingJoinScheme = null;
    }

    // =================================================================
    // キャラ選択(LobbyPlayer から呼ばれる)
    // =================================================================

    public string GetNextCharacter(string currentID)
    {
        return GetCharacterByOffset(currentID, 1);
    }

    public string GetPreviousCharacter(string currentID)
    {
        return GetCharacterByOffset(currentID, -1);
    }

    private string GetCharacterByOffset(string currentID, int offset)
    {
        if (characterIDs == null || characterIDs.Length == 0) return currentID;

        int index = System.Array.IndexOf(characterIDs, currentID);
        if (index < 0) index = 0;

        int next = (index + offset + characterIDs.Length) % characterIDs.Length;
        return characterIDs[next];
    }

    // =================================================================
    // 開始 / 遷移
    // =================================================================

    // 人間の席が1人以上いて、全員が準備完了
    private bool CanStart()
    {
        if (PlayerCount == 0) return false;

        foreach (var s in seats)
        {
            if (s != null && !s.IsReady) return false;
        }
        return true;
    }

    // 開始できるのは1Pだけ
    public void RequestStart(LobbyPlayer requester)
    {
        if (requester.PlayerNumber != 1) return;

        if (!CanStart())
        {
            ShowNotice("全員の準備完了が必要です");
            return;
        }

        WriteSession();
        SceneManager.LoadScene(matchSceneName);
    }

    // 席の情報を GameSession に書き込む(空席は CPU またはダミーで埋める)
    private void WriteSession()
    {
        for (int i = 0; i < GameSession.Seats.Length; i++)
        {
            if (seats[i] != null)
            {
                GameSession.Seats[i] = seats[i].CreateSessionData();
            }
            else if (GameSession.Entry == EntryType.Training)
            {
                GameSession.Seats[i] = new PlayerSessionData
                {
                    playerNumber = i + 1,
                    controlType = PlayerControlType.Dummy,
                    characterID = GameSession.DefaultDummyCharacterID
                };
            }
            else
            {
                GameSession.Seats[i] = new PlayerSessionData
                {
                    playerNumber = i + 1,
                    controlType = PlayerControlType.CPU,
                    characterID = GameSession.DefaultCpuCharacterID
                };
            }
        }
    }

    private void ReturnToTitle()
    {
        GameSession.ResetSession();
        SceneManager.LoadScene(titleSceneName);
    }

    // =================================================================
    // 表示
    // =================================================================

    private void ShowNotice(string message)
    {
        if (noticeText == null) return;

        noticeText.text = message;
        noticeTimer = noticeSeconds;
    }

    public void RefreshLobbyUI()
    {
        if (player1InfoText != null) player1InfoText.text = SeatText(0);
        if (player2InfoText != null) player2InfoText.text = SeatText(1);

        if (playerCountText != null)
        {
            playerCountText.text = $"参加人数：{PlayerCount}人";
        }

        if (modeInfoText != null)
        {
            modeInfoText.text = PlayerCount == 0
                ? "モード：待機中"
                : $"モード：{ModeLabel(GameSession.DetermineMode(GameSession.Entry, PlayerCount))}";
        }

        if (startInfoText != null)
        {
            if (PlayerCount == 0) startInfoText.text = "ボタンを押して参加してください";
            else if (CanStart()) startInfoText.text = "1P：スタートボタンで開始";
            else startInfoText.text = "プレイヤーの準備を待っています";
        }
    }

    private string SeatText(int index)
    {
        LobbyPlayer p = seats[index];

        if (p != null)
        {
            return $"P{index + 1}：{p.SelectedCharacterID}\n" +
                   (p.IsReady ? "準備完了" : "選択中") +
                   $"\n({p.SchemeName})";
        }

        if (index == 0) return "P1：ボタンで参加";

        if (GameSession.Entry == EntryType.Training) return "P2：ダミー";

        return $"P2：CPU({GameSession.DefaultCpuCharacterID})\nボタンで参加";
    }

    private static string ModeLabel(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.Versus: return "対戦";
            case GameMode.Solo: return "一人用";
            case GameMode.Training: return "トレーニング";
            default: return mode.ToString();
        }
    }

    // =================================================================
    // デバッグ用(コントローラーが無い環境で、UIのボタンから操作する場合)
    // =================================================================

    public void P1NextCharacter() => seats[0]?.SelectNextCharacter();
    public void P1PreviousCharacter() => seats[0]?.SelectPreviousCharacter();
    public void P1ToggleReady() => seats[0]?.ToggleReady();

    public void P2NextCharacter() => seats[1]?.SelectNextCharacter();
    public void P2PreviousCharacter() => seats[1]?.SelectPreviousCharacter();
    public void P2ToggleReady() => seats[1]?.ToggleReady();
}