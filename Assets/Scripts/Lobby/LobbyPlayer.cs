using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>ロビーで使う入力スキーム名とキー設定。</summary>
public static class LobbySchemes
{
    public const string GamepadScheme = "Gamepad";
    public const string Keyboard1Scheme = "Keyboard1";   // 左手側(開発用)
    public const string Keyboard2Scheme = "Keyboard2";   // 右手側(開発用)

    public struct KeySet
    {
        public Key left, right, confirm, cancel, start;
    }

    public static readonly KeySet Keyboard1Keys = new KeySet
    {
        left = Key.A,
        right = Key.D,
        confirm = Key.F,
        cancel = Key.G,
        start = Key.Space
    };

    public static readonly KeySet Keyboard2Keys = new KeySet
    {
        left = Key.LeftArrow,
        right = Key.RightArrow,
        confirm = Key.Slash,
        cancel = Key.Period,
        start = Key.Enter
    };

    public static KeySet GetKeys(string scheme)
    {
        return scheme == Keyboard2Scheme ? Keyboard2Keys : Keyboard1Keys;
    }
}

/// <summary>
/// ロビーに参加している1人分。MonoBehaviour ではなく普通のクラスで、
/// 自分のコントローラーの入力を直接読む(PlayerInput / プレハブは不要)。
/// 毎フレーム LobbyManager から Tick() が呼ばれる。
/// </summary>
public class LobbyPlayer
{
    public int PlayerNumber { get; private set; }
    public InputDevice Device { get; }
    public string SchemeName { get; }
    public string SelectedCharacterID { get; private set; }
    public bool IsReady { get; private set; }

    private readonly LobbyManager manager;
    private readonly int joinedFrame;
    private bool stickNeutral = true;   // スティックが中立に戻っているか

    public LobbyPlayer(
        LobbyManager manager,
        InputDevice device,
        string schemeName,
        int playerNumber,
        string characterID)
    {
        this.manager = manager;
        Device = device;
        SchemeName = schemeName;
        PlayerNumber = playerNumber;
        SelectedCharacterID = characterID;

        // 参加ボタンを押したフレームの入力を、そのまま「決定」と誤認しないための印
        joinedFrame = Time.frameCount;
    }

    public void SetPlayerNumber(int number)
    {
        PlayerNumber = number;
    }

    public void SetReady(bool ready)
    {
        IsReady = ready;
    }

    // ---- キャラ選択 / 準備完了(コントローラー操作とデバッグ用ボタンの両方から使う) ----

    public void SelectNextCharacter()
    {
        if (IsReady) return;

        SelectedCharacterID = manager.GetNextCharacter(SelectedCharacterID);
        manager.RefreshLobbyUI();
    }

    public void SelectPreviousCharacter()
    {
        if (IsReady) return;

        SelectedCharacterID = manager.GetPreviousCharacter(SelectedCharacterID);
        manager.RefreshLobbyUI();
    }

    public void ToggleReady()
    {
        IsReady = !IsReady;
        manager.RefreshLobbyUI();
    }

    // ---- 入力の読み取り ----

    public void Tick()
    {
        if (Time.frameCount <= joinedFrame) return;

        ReadInput(out int move, out bool confirm, out bool cancel, out bool start);

        if (move > 0) SelectNextCharacter();
        else if (move < 0) SelectPreviousCharacter();

        if (confirm && !IsReady)
        {
            IsReady = true;
            manager.RefreshLobbyUI();
        }

        if (cancel)
        {
            if (IsReady)
            {
                // 1段階目: 準備完了の取り消し
                IsReady = false;
                manager.RefreshLobbyUI();
            }
            else
            {
                // 2段階目: 席を離れる
                manager.RequestLeave(this);
                return;
            }
        }

        if (start)
        {
            manager.RequestStart(this);
        }
    }

    private void ReadInput(out int move, out bool confirm, out bool cancel, out bool start)
    {
        move = 0;
        confirm = false;
        cancel = false;
        start = false;

        if (Device is Gamepad pad)
        {
            // 十字キーは押した瞬間に1回
            if (pad.dpad.left.wasPressedThisFrame) move = -1;
            else if (pad.dpad.right.wasPressedThisFrame) move = 1;

            // スティックは倒した瞬間に1回だけ反応し、中立に戻るまで再反応しない
            float x = pad.leftStick.x.ReadValue();
            if (Mathf.Abs(x) < 0.3f)
            {
                stickNeutral = true;
            }
            else if (stickNeutral && Mathf.Abs(x) > 0.6f)
            {
                stickNeutral = false;
                move = x > 0f ? 1 : -1;
            }

            confirm = pad.buttonSouth.wasPressedThisFrame;   // A
            cancel = pad.buttonEast.wasPressedThisFrame;     // B
            start = pad.startButton.wasPressedThisFrame;
        }
        else if (Device is Keyboard kb)
        {
            var keys = LobbySchemes.GetKeys(SchemeName);

            if (kb[keys.left].wasPressedThisFrame) move = -1;
            else if (kb[keys.right].wasPressedThisFrame) move = 1;

            confirm = kb[keys.confirm].wasPressedThisFrame;
            cancel = kb[keys.cancel].wasPressedThisFrame;
            start = kb[keys.start].wasPressedThisFrame;
        }
    }

    /// <summary>試合シーンへ渡す席の情報を作る。</summary>
    public PlayerSessionData CreateSessionData()
    {
        return new PlayerSessionData
        {
            playerNumber = PlayerNumber,
            controlType = PlayerControlType.Human,
            characterID = SelectedCharacterID,
            schemeName = SchemeName,
            device = Device
        };
    }
}