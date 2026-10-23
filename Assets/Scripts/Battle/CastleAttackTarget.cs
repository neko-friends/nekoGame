using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 城の攻撃対象となるオブジェクトに追加する。
/// onDamaged には、既存のプレイヤーHP管理スクリプトの TakeDamage(int) などを登録する。
/// teamId は城と同じ値なら味方、異なる値なら敵として扱う。
/// </summary>
public class CastleAttackTarget : MonoBehaviour
{
    [Header("陣営設定")]
    [SerializeField] private int teamId = 2;

    [Header("被ダメージ通知")]
    [SerializeField] private UnityEvent<int> onDamaged;

    public int TeamId => teamId;

    public void TakeDamage(int damage)
    {
        if (damage <= 0) return;

        onDamaged?.Invoke(damage);
        Debug.Log($"{gameObject.name} は城から {damage} ダメージを受けた。");
    }
}
