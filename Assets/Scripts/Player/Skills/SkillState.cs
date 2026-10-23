using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 必殺技の実行中の状態。SkillData(フレーム指定のデータ)の通りに、
/// 文字・音声の表示、上昇、当たり判定、画像オブジェクト(拳など)を進める。操作は受け付けない。
/// </summary>
public class SkillState : CharacterState
{
    // 画像と当たり判定を持つオブジェクト(拳など)1つ分の、出ている間の情報
    private class ActiveEffect
    {
        public SkillEffect def;
        public SkillEffectEntry entry;      // 画像の登録(無ければ null)
        public GameObject go;
        public SpriteRenderer renderer;
        public int age;                     // 出てから何フレーム目か
        public int facing;                  // 出した時点の向き
        public readonly HashSet<PlayerController> alreadyHit = new HashSet<PlayerController>();
    }

    private SkillData skill;
    private float savedGravityScale;
    private float riseTotalDistance;

    // 判定グループごとの「すでに当てた相手」
    private readonly Dictionary<int, HashSet<PlayerController>> hitGroups =
        new Dictionary<int, HashSet<PlayerController>>();

    private readonly List<ActiveEffect> effects = new List<ActiveEffect>();

    public SkillState(PlayerController owner) : base(owner) { }

    public override Color DebugColor => new Color(1f, 0.5f, 0f);   // オレンジ

    /// <summary>いま出している技の種類(アニメーションが画像を選ぶのに使う)。</summary>
    public SkillSlot CurrentSlot => skill != null ? skill.slot : SkillSlot.Neutral;

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
        DestroyAllEffects();

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

        // 当たり判定(技本体)
        foreach (SkillHitWindow window in skill.hits)
        {
            if (Frames >= window.startFrame && Frames <= window.endFrame)
            {
                CheckHits(window);
            }
        }

        // 画像オブジェクト(拳など): 出す → 動かす → 判定する → 消す
        SpawnEffects();
        UpdateEffects();

        if (Frames >= skill.totalFrames)
        {
            owner.Machine.Change(owner.Neutral);
        }
    }

    public override void Exit()
    {
        owner.Body.gravityScale = savedGravityScale;
        owner.ActiveSkillBoxes.Clear();
        DestroyAllEffects();
    }

    // =================================================================
    // 上昇
    // =================================================================

    // 上昇期間は一定の速さで上がり、それ以外は止まる
    private float CurrentRiseSpeed()
    {
        if (Frames < skill.riseStartFrame || Frames > skill.riseEndFrame) return 0f;

        int riseFrames = skill.riseEndFrame - skill.riseStartFrame + 1;
        return riseTotalDistance / (riseFrames * Time.fixedDeltaTime);
    }

    // =================================================================
    // 当たり判定(技本体の四角)
    // =================================================================

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

        HitInBox(center, size, alreadyHit);
    }

    // 範囲の中の相手に、まだ当てていなければ当てる
    private void HitInBox(Vector2 center, Vector2 size, HashSet<PlayerController> alreadyHit)
    {
        Collider2D[] found = Physics2D.OverlapBoxAll(center, size, 0f);

        foreach (Collider2D col in found)
        {
            PlayerController target = col.GetComponentInParent<PlayerController>();

            if (target == null || target == owner) continue;
            if (!alreadyHit.Add(target)) continue;

            target.ReceiveHit(BuildHit(target));
        }
    }

    // =================================================================
    // 画像オブジェクト(拳など)
    // =================================================================

    private void SpawnEffects()
    {
        foreach (SkillEffect def in skill.effects)
        {
            if (def.frame != Frames) continue;

            SkillEffectEntry entry = null;
            var view = owner.GetComponent<SkillEffectView>();
            if (view != null) view.TryGetEntry(def.effectKey, out entry);

            var go = new GameObject("SkillEffect_" + def.effectKey);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = entry != null ? entry.sortingOrder : 50;
            sr.flipX = owner.Facing < 0;   // 絵は右向きに描いてある前提
            go.transform.localScale = Vector3.one * (entry != null ? entry.drawScale : 1f);

            effects.Add(new ActiveEffect
            {
                def = def,
                entry = entry,
                go = go,
                renderer = sr,
                age = 0,
                facing = owner.Facing
            });
        }
    }

    private void UpdateEffects()
    {
        Bounds body = owner.BodyBounds;

        for (int i = effects.Count - 1; i >= 0; i--)
        {
            ActiveEffect e = effects[i];
            e.age++;

            // 寿命が来たら消す
            if (e.age > e.def.lifeFrames)
            {
                DestroyEffect(e);
                effects.RemoveAt(i);
                continue;
            }

            // 位置: 猫についていく
            Vector2 position = (Vector2)body.center + new Vector2(
                e.def.offset.x * body.size.x * e.facing,
                e.def.offset.y * body.size.y);
            e.go.transform.position = new Vector3(position.x, position.y, owner.transform.position.z);

            // 画像
            if (e.entry != null)
            {
                e.renderer.sprite = e.entry.GetSpriteAtAge(e.age);
            }

            // 当たり判定: 画像の大きさに合わせる。画像が無いときだけ決め打ちの大きさ
            Vector2 center;
            Vector2 size;

            if (e.renderer.sprite != null)
            {
                Bounds b = e.renderer.bounds;
                Vector2 hitScale = e.entry != null ? e.entry.hitScale : Vector2.one;
                center = b.center;
                size = new Vector2(b.size.x * hitScale.x, b.size.y * hitScale.y);
            }
            else
            {
                center = position;
                size = new Vector2(e.def.hitSize.x * body.size.x, e.def.hitSize.y * body.size.y);
            }

            owner.ActiveSkillBoxes.Add((center, size));
            HitInBox(center, size, e.alreadyHit);
        }
    }

    private void DestroyAllEffects()
    {
        foreach (ActiveEffect e in effects) DestroyEffect(e);
        effects.Clear();
    }

    private static void DestroyEffect(ActiveEffect e)
    {
        if (e.go != null) Object.Destroy(e.go);
    }

    // =================================================================
    // ダメージ
    // =================================================================

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