
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通常攻撃の当たり判定。
/// PlayerController を持つ相手には通常のヒット処理を行い、
/// StageObjectDurability を持つ城などには耐久ダメージを与える。
/// このスクリプトは PlayerController と同じ GameObject に付ける。
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class AttackHitbox : MonoBehaviour
{
    [Header("判定の位置と大きさ（右向き時。猫の中心からの相対）")]
    [SerializeField] private Vector2 offset = new Vector2(0.8f, 0f);
    [SerializeField] private Vector2 size = new Vector2(0.8f, 0.8f);

    [Header("当てる対象のレイヤー（城のレイヤーも含める）")]
    [SerializeField] private LayerMask targetLayers = ~0;

    private PlayerController owner;
    private readonly HashSet<PlayerController> alreadyHitPlayers = new HashSet<PlayerController>();
    private readonly HashSet<StageObjectDurability> alreadyHitStageObjects = new HashSet<StageObjectDurability>();
    private bool wasActive;

    private void Awake()
    {
        owner = GetComponent<PlayerController>();
    }

    private void FixedUpdate()
    {
        if (owner == null) return;

        bool active = owner.AttackHitboxActive;

        // 攻撃判定が有効になった瞬間に、この攻撃で当てた相手の記録をリセット
        if (active && !wasActive)
        {
            alreadyHitPlayers.Clear();
            alreadyHitStageObjects.Clear();
        }

        wasActive = active;
        if (!active) return;

        Collider2D[] found = Physics2D.OverlapBoxAll(GetCenter(), size, 0f, targetLayers);

        foreach (Collider2D col in found)
        {
            if (col == null) continue;

            // まずプレイヤーを探す
            PlayerController targetPlayer = col.GetComponentInParent<PlayerController>();
            if (targetPlayer != null)
            {
                if (targetPlayer == owner) continue;
                if (!alreadyHitPlayers.Add(targetPlayer)) continue;

                targetPlayer.ReceiveHit(BuildHit());
                Debug.Log($"{owner.name} の通常攻撃がプレイヤー {targetPlayer.name} に命中");
                continue;
            }

            // プレイヤーでなければ、城などの耐久オブジェクトを探す
            StageObjectDurability stageObject = col.GetComponentInParent<StageObjectDurability>();
            if (stageObject == null || stageObject.IsDestroyed) continue;
            if (!alreadyHitStageObjects.Add(stageObject)) continue;

            CharacterParams p = owner.Params;
            int damage = Mathf.Max(1, Mathf.RoundToInt(p.atk * p.normalAttackMultiplier));
            stageObject.TakeDamage(damage);
            Debug.Log($"{owner.name} の通常攻撃が {stageObject.name} に命中！ ダメージ: {damage}");
        }
    }

    private Vector2 GetCenter()
    {
        int facing = owner != null ? owner.Facing : 1;
        Vector2 relativeOffset = new Vector2(offset.x * facing, offset.y);
        return (Vector2)transform.position + relativeOffset;
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
            guardDamage = damage
        };
    }

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

