using UnityEngine;

/// <summary>
/// 城の定期攻撃。
/// 城と同じ陣営(teamId)の対象には攻撃しない。
/// 初期値は仮設定なので、仕様に合わせてInspectorで調整する。
/// </summary>
public class CastleAttackController : MonoBehaviour
{
    [Header("陣営設定")]
    [SerializeField] private int teamId = 1;

    [Header("攻撃設定（仮設定・Inspectorで調整）")]
    [SerializeField, Min(0.1f)] private float attackInterval = 2f;
    [SerializeField, Min(0.1f)] private float attackRange = 8f;
    [SerializeField, Min(1)] private int attackDamage = 10;

    [Header("対象設定")]
    [SerializeField] private LayerMask targetLayers = ~0;

    [Header("攻撃演出（任意）")]
    [SerializeField] private GameObject attackEffectPrefab;
    [SerializeField] private Transform attackPoint;

    private float nextAttackTime;

    private void Update()
    {
        if (Time.time < nextAttackTime) return;

        CastleAttackTarget target = FindNearestEnemy();
        if (target == null) return;

        target.TakeDamage(attackDamage);
        nextAttackTime = Time.time + attackInterval;

        if (attackEffectPrefab != null)
        {
            Vector3 effectPosition = attackPoint != null
                ? attackPoint.position
                : transform.position;

            Instantiate(attackEffectPrefab, effectPosition, Quaternion.identity);
        }

        Debug.Log($"{gameObject.name} が {target.gameObject.name} を攻撃。ダメージ: {attackDamage}");
    }

    private CastleAttackTarget FindNearestEnemy()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            attackRange,
            targetLayers
        );

        CastleAttackTarget nearest = null;
        float nearestDistanceSqr = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;

            CastleAttackTarget candidate = hit.GetComponentInParent<CastleAttackTarget>();
            if (candidate == null) continue;
            if (candidate.TeamId == teamId) continue;

            float distanceSqr = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr >= nearestDistanceSqr) continue;

            nearestDistanceSqr = distanceSqr;
            nearest = candidate;
        }

        return nearest;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
