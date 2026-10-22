using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通常攻撃の当たり判定。猫のルート(PlayerController と同じオブジェクト)に付ける。
/// ・AttackState が AttackHitboxActive を true にしている間だけ判定する
/// ・1回の攻撃につき、同じ相手には1回だけ当てる
/// ・位置と大きさは「右向きのとき」の値を入れる。左向きは自動で反転する
/// ・ダメージ = atk × 通常攻撃の技補正値(企画書)
/// Collider2D を使わず範囲を直接調べるので、PlayerController のアイテム判定などに影響しない。
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class AttackHitbox : MonoBehaviour
{
    [Header("判定の位置と大きさ(右向きのとき。猫の中心からの相対)")]
    [SerializeField] private Vector2 offset = new Vector2(0.8f, 0f);
    [SerializeField] private Vector2 size = new Vector2(0.8f, 0.8f);

    [Header("当てる対象のレイヤー(既定は全部。自分は自動で除外)")]
    [SerializeField] private LayerMask targetLayers = ~0;

    private PlayerController owner;
    private readonly HashSet<PlayerController> alreadyHit = new HashSet<PlayerController>();
    private bool wasActive;

    private void Awake()
    {
        owner = GetComponent<PlayerController>();
    }

    private void FixedUpdate()
    {
        bool active = owner.AttackHitboxActive;

        // 判定が有効になった最初のフレームで「当てた相手」の記録をリセット
        if (active && !wasActive) alreadyHit.Clear();
        wasActive = active;

        if (!active) return;

        Collider2D[] found = Physics2D.OverlapBoxAll(GetCenter(), size, 0f, targetLayers);

        foreach (Collider2D col in found)
        {
            PlayerController target = col.GetComponentInParent<PlayerController>();

            if (target == null || target == owner) continue;
            if (!alreadyHit.Add(target)) continue;   // すでに当てた相手

            target.ReceiveHit(BuildHit());
        }
    }

    private Vector2 GetCenter()
    {
        // 左向きのときは横の位置を反転
        Vector2 o = new Vector2(offset.x * owner.Facing, offset.y);
        return (Vector2)transform.position + o;
    }

    private HitInfo BuildHit()
    {
        CharacterParams p = owner.Params;
        float damage = p.atk * p.normalAttackMultiplier;

        return new HitInfo
        {
            damage = damage,
            knockback = new Vector2(p.attackKnockbackX * owner.Facing, p.attackKnockbackY),
            hitstunFrames = p.attackHitstunFrames,
            guardDamage = damage   // 企画書: ダメージを受けたら、その分ガード耐久値を減らす
        };
    }

    // シーンビューで判定の範囲を確認できる(選択中は常に表示、攻撃中は塗りつぶし)
    private void OnDrawGizmosSelected()
    {
        int facing = owner != null ? owner.Facing : 1;
        Vector2 center = (Vector2)transform.position + new Vector2(offset.x * facing, offset.y);

        Gizmos.color = new Color(1f, 0f, 0f, 0.9f);
        Gizmos.DrawWireCube(center, size);
    }

    private void OnDrawGizmos()
    {
        if (owner == null || !owner.AttackHitboxActive) return;

        Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
        Gizmos.DrawCube(GetCenter(), size);
    }
}
