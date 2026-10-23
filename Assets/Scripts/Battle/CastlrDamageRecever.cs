
using UnityEngine;

/// <summary>
/// プレイヤーの攻撃から城へのダメージを受け取る。
/// </summary>
[RequireComponent(typeof(StageObjectDurability))]
public class CastleDamageReceiver : MonoBehaviour
{
    [SerializeField, Min(1)]
    private int damageMultiplier = 1;

    private StageObjectDurability durability;

    private void Awake()
    {
        durability = GetComponent<StageObjectDurability>();
    }

    public void ReceiveDamage(int damage)
    {
        if (durability == null)
        {
            durability = GetComponent<StageObjectDurability>();
        }

        if (durability == null || durability.IsDestroyed)
        {
            Debug.LogWarning(
                $"{name}: StageObjectDurability が見つかりません。"
            );
            return;
        }

        int finalDamage = Mathf.Max(1, damage * damageMultiplier);
        durability.TakeDamage(finalDamage);
    }
}
