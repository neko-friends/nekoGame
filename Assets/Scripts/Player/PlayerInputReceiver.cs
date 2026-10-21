using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 入力を受け取って記録するだけのコンポーネント。
/// PlayerInput(Invoke Unity Events)から呼ばれ、ボタンの状態を保持する。
/// キャラの動きは一切知らない。キャラ側(PlayerController)が毎フレーム読みに来る。
/// </summary>
public class PlayerInputReceiver : MonoBehaviour
{
    // 移動入力(左スティック / 十字キー)
    public Vector2 MoveInput { get; private set; }

    // 各ボタンの状態。ボタンを増やすときはここに1行足し、下の All にも加える
    public ButtonInput Jump { get; } = new ButtonInput();
    public ButtonInput Throw { get; } = new ButtonInput();   // 拾う / 投げる
    public ButtonInput Attack { get; } = new ButtonInput();
    public ButtonInput Special { get; } = new ButtonInput();
    public ButtonInput Guard { get; } = new ButtonInput();
    public ButtonInput Pose { get; } = new ButtonInput();

    private ButtonInput[] gameplayButtons;

    private void Awake()
    {
        gameplayButtons = new[] { Jump, Throw, Attack, Special, Guard };
    }

    /// <summary>ゲームの1フレーム(FixedUpdate)ごとに呼ぶ。入力バッファの時間を進める。</summary>
    public void AdvanceFrame()
    {
        foreach (var b in gameplayButtons) b.AdvanceFrame();
        Pose.AdvanceFrame();
    }

    /// <summary>溜まっている先行入力を捨てる(被弾・スタン時など)。</summary>
    public void ClearBuffers()
    {
        foreach (var b in gameplayButtons) b.ClearBuffer();
    }

    // ---- 以下は PlayerInput のイベントから呼ばれる ----

    public void OnMove(InputAction.CallbackContext context)
    {
        // 押している間は値が入り、離すと (0,0) になる
        MoveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context) => Jump.Record(context);
    public void OnThrow(InputAction.CallbackContext context) => Throw.Record(context);
    public void OnAttack(InputAction.CallbackContext context) => Attack.Record(context);
    public void OnSpecial(InputAction.CallbackContext context) => Special.Record(context);
    public void OnGuard(InputAction.CallbackContext context) => Guard.Record(context);
    public void OnPose(InputAction.CallbackContext context) => Pose.Record(context);
}

/// <summary>
/// 1つのボタンの状態を記録するクラス。
/// ・IsHeld          : 今押されているか(ガードなど「押している間」用)
/// ・TryConsumePress : 「押された」を1回だけ受け取る(フレーム単位の猶予つき=入力バッファ)
/// </summary>
public class ButtonInput
{
    public bool IsHeld { get; private set; }

    private int age = int.MaxValue;   // 押されてから何フレーム経ったか
    private bool consumed = true;     // すでに使われたか

    public void Record(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            IsHeld = true;
            age = 0;
            consumed = false;
        }
        else if (context.canceled)
        {
            IsHeld = false;
        }
    }

    public void AdvanceFrame()
    {
        if (!consumed && age < int.MaxValue) age++;
    }

    /// <summary>
    /// 押された入力を1回だけ受け取る。
    /// bufferFrames フレーム以内に押されていて、まだ使われていなければ true。
    /// </summary>
    public bool TryConsumePress(int bufferFrames)
    {
        if (consumed || age > bufferFrames) return false;

        consumed = true;
        return true;
    }

    public void ClearBuffer()
    {
        consumed = true;
    }
}
