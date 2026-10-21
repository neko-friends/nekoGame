using System;
using UnityEngine;

/// <summary>
/// 状態の共通の形。状態ごとにこれを継承したクラスを作る。
///   Enter : 状態に入った瞬間に1回
///   Tick  : 入っている間、毎フレーム(FixedUpdate)
///   Exit  : 状態を出る瞬間に1回
/// Frames は「この状態に入ってから何フレーム目か」(Tick 内では1から始まる)。
/// </summary>
public abstract class CharacterState
{
    protected readonly PlayerController owner;

    protected CharacterState(PlayerController owner)
    {
        this.owner = owner;
    }

    public int Frames { get; internal set; }

    public virtual string Name => GetType().Name.Replace("State", "");

    // デバッグ表示用の色(仮の絵を状態ごとに塗り分けて確認する用)
    public virtual Color DebugColor => Color.white;

    public virtual void Enter() { }
    public virtual void Tick() { }
    public virtual void Exit() { }
}

/// <summary>
/// 現在の状態を1つだけ持ち、切り替えと更新を行う。
/// 注意: Tick の中で Change を呼んだら、その Tick の処理はすぐ return すること。
/// </summary>
public class StateMachine
{
    public CharacterState Current { get; private set; }

    // 状態が変わったときの通知(前の状態, 次の状態)。アニメーションや効果音の接続口。
    public event Action<CharacterState, CharacterState> Changed;

    public void Change(CharacterState next)
    {
        if (next == null) return;

        CharacterState previous = Current;

        previous?.Exit();

        Current = next;
        Current.Frames = 0;
        Current.Enter();

        Changed?.Invoke(previous, next);
    }

    public void Tick()
    {
        if (Current == null) return;

        Current.Frames++;
        Current.Tick();
    }
}
