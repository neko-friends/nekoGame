using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>ロビー(コントローラー識別画面)で使う入力スキーム名とキー設定。</summary>
public static class LobbySchemes
{
    public const string GamepadScheme = "Gamepad";
    public const string Keyboard1Scheme = "Keyboard1";   // 左手側(開発用)
    public const string Keyboard2Scheme = "Keyboard2";   // 右手側(開発用)

    // confirm = 参加(Bボタン相当) / start = 準備完了(メニューボタン相当)
    // left / right / cancel は現在の識別画面では使わない(他の画面での再利用用に残している)
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
