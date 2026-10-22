using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 必殺技の実行中の状態。SkillData(フレーム指定のデータ)の通りに、
/// 文字・音声の表示、上昇、当たり判定を進める。操作は受け付けない。
/// </summary>
public class SkillState : CharacterState
{
    private SkillData skill;
    private float savedGravityScale;
    private float riseTotalDistance;

    // 判定グループごとの「すでに当てた相手」
    private readonly Dictionary<int, HashSet<PlayerController>> hitGroups =
        new Dictionary<int, HashSet<PlayerController>>();

    public SkillState(PlayerController owner) : base(owner) { }

    public override Color DebugColor => new Color(1f, 0.5f, 0f);   // オレンジ

    /// <summary>入る前に、出す技をセットする。データが無い技なら false(状態は変えない)。</summary>
    public bool Begin(SpecialDirection direction)
    {
        SkillSlot slot;
        switch (direction)
        {
            case SpecialDirection.Forward: slot = SkillSlot.Side; break;
            case SpecialDirection.Up: slot = SkillSlot.Up; break;
            case SpecialDirection.Down: slot = SkillSlot.Down; break;
            default: slot = SkillSlot.Neutral; break;
        }

        skill = SkillLibrary.Get(owner.CharacterId, slot);
        return skill != null;
    }

    public override void Enter()
    {
        hitGroups.Clear();
        owner.AttackHitboxActive = false;
        owner.SetVelocityX(0f);

        savedGravityScale = owner.Body.gravityScale;
        riseTotalDistance = owner.BodyBounds.size.y * skill.riseBodyHeights;

        // 上昇する技は、重力を切って自分で上下の動きを決める
        if (skill.riseBodyHeights > 0f)
        {
            owner.Body.gravityScale = 0f;
            owner.SetVelocity(Vector2.zero);
        }
    }

    public override void Tick()
    {
        owner.ActiveSkillBoxes.Clear();

        // 文字と音声
        foreach (SkillCue cue in skill.cues)
        {
            if (cue.frame == Frames) owner.RaiseCue(cue.text);
        }

        // 移動(横は止める。上昇する技は縦の速度も自分で決める)
        if (skill.riseBodyHeights > 0f)
        {
            owner.SetVelocity(new Vector2(0f, CurrentRiseSpeed()));
        }
        else
        {
            owner.SetVelocityX(0f);
        }

        // 当たり判定
        foreach (SkillHitWindow window in skill.hits)
        {
            if (Frames >= window.startFrame && Frames <= window.endFrame)
            {
                CheckHits(window);
            }
        }

        if (Frames >= skill.totalFrames)
        {
            owner.Machine.Change(owner.Neutral);
        }
    }

    public override void Exit()
    {
        owner.Body.gravityScale = savedGravityScale;
        owner.ActiveSkillBoxes.Clear();
    }

    // 上昇期間は一定の速さで上がり、それ以外は止まる
    private float CurrentRiseSpeed()
    {
        if (Frames < skill.riseStartFrame || Frames > skill.riseEndFrame) return 0f;

        int riseFrames = skill.riseEndFrame - skill.riseStartFrame + 1;
        return riseTotalDistance / (riseFrames * Time.fixedDeltaTime);
    }

    private void CheckHits(SkillHitWindow window)
    {
        Bounds body = owner.BodyBounds;

        Vector2 center = (Vector2)body.center + new Vector2(
            window.offset.x * body.size.x * owner.Facing,
            window.offset.y * body.size.y);
        Vector2 size = new Vector2(window.size.x * body.size.x, window.size.y * body.size.y);

        owner.ActiveSkillBoxes.Add((center, size));   // シーンビューで確認する用

        if (!hitGroups.TryGetValue(window.group, out HashSet<PlayerController> alreadyHit))
        {
            alreadyHit = new HashSet<PlayerController>();
            hitGroups[window.group] = alreadyHit;
        }

        Collider2D[] found = Physics2D.OverlapBoxAll(center, size, 0f);

        foreach (Collider2D col in found)
        {
            PlayerController target = col.GetComponentInParent<PlayerController>();

            if (target == null || target == owner) continue;
            if (!alreadyHit.Add(target)) continue;

            target.ReceiveHit(BuildHit(target));
        }
    }

    private HitInfo BuildHit(PlayerController target)
    {
        CharacterParams p = owner.Params;

        float multiplier;
        switch (skill.slot)
        {
            case SkillSlot.Side: multiplier = p.sideSpecialMultiplier; break;
            case SkillSlot.Up: multiplier = p.upSpecialMultiplier; break;
            case SkillSlot.Down: multiplier = p.downSpecialMultiplier; break;
            default: multiplier = p.neutralSpecialMultiplier; break;
        }

        float damage = p.atk * multiplier;

        // 相手のいる側へ飛ばす(真上・真下で重なったときは向いている側)
        float dir = Mathf.Sign(target.transform.position.x - owner.transform.position.x);
        if (dir == 0f) dir = owner.Facing;

        return new HitInfo
        {
            damage = damage,
            knockback = new Vector2(p.attackKnockbackX * dir, p.attackKnockbackY),
            hitstunFrames = p.attackHitstunFrames,
            guardDamage = damage   // 企画書: ダメージを受けたら、その分ガード耐久値を減らす
        };
    }
}
