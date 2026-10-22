using UnityEngine;

// =====================================================================
// 通常: 待機・走り・ジャンプ・空中移動をまとめた基本状態。
// 他の状態へ移る「入口」はここに集約している。
// =====================================================================
public class NeutralState : CharacterState
{
    public NeutralState(PlayerController owner) : base(owner) { }

    public override Color DebugColor => Color.white;

    public override void Tick()
    {
        var c = owner.Controls;
        var p = owner.Params;
        int buffer = p.inputBufferFrames;

        // 移動
        Vector2 move = c.MoveInput;
        owner.Face(move.x);
        owner.SetVelocityX(move.x * p.moveSpeed);

        // ジャンプ(残り回数があるときだけ入力を消費する。無ければ猶予の間、入力を保持)
        if (owner.JumpsLeft > 0 && c.Jump.TryConsumePress(buffer))
        {
            owner.TryJump();
        }

        // 通常攻撃
        if (c.Attack.TryConsumePress(buffer))
        {
            owner.Machine.Change(owner.Attack);
            return;
        }

        // 必殺技(中身は技データの接続後に実装。今は方向の判定だけ確認できる)
        if (c.Special.TryConsumePress(buffer))
        {
            Debug.Log($"必殺技(未実装) 方向: {owner.ReadSpecialDirection()}");
        }

        // アイテムを拾う / 投げる
        if (c.Throw.TryConsumePress(buffer))
        {
            owner.ThrowOrPickup();
        }

        // ガード(空中でも可)
        if (c.Guard.IsHeld && owner.GuardDurability > 0f)
        {
            owner.Machine.Change(owner.Guard);
        }
    }
}

// =====================================================================
// 通常攻撃(単発): 発生 → 持続 → 硬直。フレーム数は CharacterParams で設定。
// 持続中だけ AttackHitboxActive が true になる(判定の実装は次のステップ)。
// =====================================================================
public class AttackState : CharacterState
{
    public AttackState(PlayerController owner) : base(owner) { }

    public override Color DebugColor => Color.yellow;

    public override void Enter()
    {
        owner.AttackHitboxActive = false;
        owner.SetVelocityX(0f);
    }

    public override void Tick()
    {
        var p = owner.Params;

        int activeStart = p.attackStartupFrames;
        int activeEnd = p.attackStartupFrames + p.attackActiveFrames;
        int total = activeEnd + p.attackRecoveryFrames;

        // 持続フレームの間だけ判定が有効
        owner.AttackHitboxActive = Frames > activeStart && Frames <= activeEnd;

        // 発生〜持続の間は少し前へ踏み込む
        bool lunging = Frames <= activeEnd;
        owner.SetVelocityX(lunging ? owner.Facing * p.attackLungeSpeed : 0f);

        if (Frames >= total)
        {
            owner.Machine.Change(owner.Neutral);
        }
    }

    public override void Exit()
    {
        owner.AttackHitboxActive = false;
    }
}

// =====================================================================
// ガード: 押している間有効。
// 企画書の仕様:
//  ・ガード開始から一定時間(既定0.5秒)後に、耐久値が徐々に減り始める
//  ・被弾で耐久値が減ったら、その後一定時間(既定0.5秒)は自然消費を止める
//  ・ガードをやめてから一定時間(既定3秒)後に回復が始まる(Exit で待ち時間をセット)
//  ・耐久値が0になると崩壊(GuardBreakState)
// =====================================================================
public class GuardState : CharacterState
{
    public GuardState(PlayerController owner) : base(owner) { }

    public override Color DebugColor => Color.cyan;

    public override void Enter()
    {
        owner.SetVelocityX(0f);
        owner.GuardDrainHoldFrames = 0;
    }

    public override void Tick()
    {
        var c = owner.Controls;
        var p = owner.Params;

        // 向きだけ変えられる。押し戻された勢いは徐々に減らす
        owner.Face(c.MoveInput.x);
        owner.SetVelocityX(owner.Body.linearVelocity.x * p.guardPushbackDecay);

        // 長押しでも耐久値は減る(開始の待ち時間が過ぎてから。被弾直後は止まる)
        bool drainStarted = Frames > owner.SecondsToFrames(p.guardDrainDelaySeconds);
        if (drainStarted && owner.GuardDrainHoldFrames <= 0)
        {
            owner.GuardDurability -= p.guardDrainPerSecond * Time.fixedDeltaTime;
        }

        if (owner.GuardDurability <= 0f)
        {
            owner.Machine.Change(owner.GuardBreak);
            return;
        }

        if (!c.Guard.IsHeld)
        {
            owner.Machine.Change(owner.Neutral);
        }
    }

    /// <summary>ガード中に攻撃を受けたとき。ダメージは防ぎ、耐久値だけが減る。</summary>
    public void Absorb(HitInfo hit)
    {
        owner.GuardDurability -= hit.guardDamage;

        // 被弾で耐久値が減ったあとは、しばらく自然消費を止める
        owner.GuardDrainHoldFrames = owner.SecondsToFrames(owner.Params.guardDrainPauseAfterHitSeconds);

        float dir = Mathf.Sign(hit.knockback.x);
        if (dir == 0f) dir = -owner.Facing;
        owner.SetVelocityX(dir * owner.Params.guardPushback);

        if (owner.GuardDurability <= 0f)
        {
            owner.Machine.Change(owner.GuardBreak);
        }
    }

    public override void Exit()
    {
        // ガードをやめた時点から、回復までの待ち時間を数え始める
        owner.GuardRegenWaitFrames = owner.SecondsToFrames(owner.Params.guardRegenDelaySeconds);
    }
}

// =====================================================================
// ガード崩壊: 耐久値が尽きたあとのスタン(既定2秒)。操作不能。
// =====================================================================
public class GuardBreakState : CharacterState
{
    private int durationFrames;

    public GuardBreakState(PlayerController owner) : base(owner) { }

    public override Color DebugColor => Color.magenta;

    public override void Enter()
    {
        owner.GuardDurability = 0f;
        owner.Controls.ClearBuffers();
        owner.SetVelocityX(0f);
        durationFrames = owner.SecondsToFrames(owner.Params.guardBreakStunSeconds);
    }

    public override void Tick()
    {
        owner.SetVelocityX(0f);

        if (Frames >= durationFrames)
        {
            // 復帰時は耐久値を回復して戻る(割合は CharacterParams。企画書では全回復)
            owner.GuardDurability = owner.Params.guardMax * owner.Params.guardAfterBreakRatio;
            owner.GuardRegenWaitFrames = 0;
            owner.Machine.Change(owner.Neutral);
        }
    }
}

// =====================================================================
// のけぞり: 被弾後の硬直。ノックバック速度を入った瞬間に一度だけ設定し、
// あとは減衰に任せる。操作不能。
// =====================================================================
public class HitstunState : CharacterState
{
    private HitInfo hit;

    public HitstunState(PlayerController owner) : base(owner) { }

    public override Color DebugColor => Color.red;

    // 状態へ入る前に、受けた攻撃の情報をセットする
    public void Begin(HitInfo incoming)
    {
        hit = incoming;
    }

    public override void Enter()
    {
        owner.Controls.ClearBuffers();
        owner.SetVelocity(hit.knockback);
    }

    public override void Tick()
    {
        owner.SetVelocityX(owner.Body.linearVelocity.x * owner.Params.knockbackDecayPerFrame);

        if (Frames >= hit.hitstunFrames)
        {
            owner.Machine.Change(owner.Neutral);
        }
    }
}

// =====================================================================
// 撃破: HPが0。復活は試合管理側が Revive() を呼んで行う想定。
// =====================================================================
public class DeadState : CharacterState
{
    public DeadState(PlayerController owner) : base(owner) { }

    public override Color DebugColor => Color.gray;

    public override void Enter()
    {
        owner.Controls.ClearBuffers();
        owner.SetVelocityX(0f);
        owner.NotifyDied();
    }
}