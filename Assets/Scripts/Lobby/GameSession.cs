using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum EntryType
{
    Normal,     // 通常(人間の人数で 対戦 / 一人用 が決まる)
    Training    // トレーニング(2P席はダミー)
}

public enum GameMode
{
    Versus,     // 人間 VS 人間
    Solo,       // 人間 VS CPU
    Training    // 人間 VS ダミー
}

public enum PlayerControlType
{
    Human,
    CPU,
    Dummy
}

/// <summary>1つの席の情報。ロビーで作り、試合シーンで読む。</summary>
[Serializable]
public class PlayerSessionData
{
    public int playerNumber;                 // 席番号(1 または 2)
    public PlayerControlType controlType;
    public string characterID;
    public string schemeName;                // "Gamepad" / "Keyboard1" / "Keyboard2"(人間のみ)

    // 使用するコントローラー。実行中のメモリ上だけで持ち、保存はしない
    [NonSerialized] public InputDevice device;
}

/// <summary>
/// シーンをまたいで残す情報の保管場所。
/// 「席の情報」が正で、モードは席の状態から導く(別々に保存しない)。
/// </summary>
public static class GameSession
{
    public const int SeatCount = 2;

    // CPU / ダミー席の既定キャラ(後でランダムや選択式にするときはここを直す)
    public const string DefaultCpuCharacterID = "Character02";
    public const string DefaultDummyCharacterID = "Character01";

    // タイトルで決める「入口の種類」
    public static EntryType Entry = EntryType.Normal;

    // ステージは固定。将来のステージ選択用に項目だけ残す
    public static string StageID = "Stage01";

    // 席のリスト(添字0 = 1P、添字1 = 2P)
    public static readonly PlayerSessionData[] Seats = new PlayerSessionData[SeatCount];

    // タイトルで決定を押したコントローラーを、ロビーの1Pとして自動参加させるための一時保持
    public static InputDevice PendingJoinDevice;
    public static string PendingJoinScheme;

    public static PlayerSessionData Player1 => Seats[0];
    public static PlayerSessionData Player2 => Seats[1];

    /// <summary>現在の席の状態から導いたモード。</summary>
    public static GameMode CurrentMode
    {
        get
        {
            int humans = 0;
            foreach (var seat in Seats)
            {
                if (seat != null && seat.controlType == PlayerControlType.Human) humans++;
            }
            return DetermineMode(Entry, humans);
        }
    }

    /// <summary>入口の種類と人間の人数から、モードを決める。</summary>
    public static GameMode DetermineMode(EntryType entry, int humanCount)
    {
        if (entry == EntryType.Training) return GameMode.Training;
        return humanCount >= 2 ? GameMode.Versus : GameMode.Solo;
    }

    /// <summary>全部を初期状態に戻す。タイトルへ戻るときに呼ぶ。</summary>
    public static void ResetSession()
    {
        Entry = EntryType.Normal;
        StageID = "Stage01";

        for (int i = 0; i < Seats.Length; i++) Seats[i] = null;

        PendingJoinDevice = null;
        PendingJoinScheme = null;
    }

    // 再生開始のたびに初期化する
    // (エディタの設定で Domain Reload を切っていても、前回の値が残らないようにする)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnLoad()
    {
        ResetSession();
    }
}