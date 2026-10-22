
using UnityEngine;

public class GateController : MonoBehaviour
{
    [Header("門の設定")]
    [SerializeField]
    private StageObjectDurability durability;

    [SerializeField]
    private Collider2D blockingCollider;

    [SerializeField]
    private GameObject visualObject;

    private void Awake()
    {
        if (durability == null)
        {
            durability = GetComponent<StageObjectDurability>();
        }
    }

    private void OnEnable()
    {
        if (durability == null)
        {
            durability = GetComponent<StageObjectDurability>();
        }

        if (durability != null)
        {
            durability.Destroyed += OnGateDestroyed;

            if (durability.IsDestroyed)
            {
                OnGateDestroyed();
            }
        }
    }

    private void OnDisable()
    {
        if (durability != null)
        {
            durability.Destroyed -= OnGateDestroyed;
        }
    }

    private void OnGateDestroyed()
    {
        // 門の通行を妨げる当たり判定を無効化
        if (blockingCollider != null)
        {
            blockingCollider.enabled = false;
        }

        // 仮の見た目を消す
        if (visualObject != null)
        {
            visualObject.SetActive(false);
        }

        Debug.Log("門が破壊された。先へ通行できる！");
    }
}
