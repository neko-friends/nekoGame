
using UnityEngine;

/// <summary>
/// MatchFlowManagerからプレイヤー参照を定期的に取得する。
/// プレイヤー参照が切れた場合も、登録済みの参照を取り直す。
/// </summary>
public class PlayerReferenceKeeper : MonoBehaviour
{
    [Header("参照するプレイヤー")]
    [SerializeField] private TeamSide targetSide;

    [Header("検索設定")]
    [SerializeField, Min(0.1f)]
    private float searchInterval = 0.5f;

    private MatchFlowManager matchFlowManager;
    private PlayerController cachedPlayer;
    private float nextSearchTime;

    public PlayerController TargetPlayer
    {
        get
        {
            RefreshReference();
            return cachedPlayer;
        }
    }

    private void Awake()
    {
        FindMatchFlowManager();
        RefreshReference();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextSearchTime &&
            cachedPlayer != null)
        {
            return;
        }

        nextSearchTime = Time.unscaledTime + searchInterval;
        RefreshReference();
    }

    private void FindMatchFlowManager()
    {
        if (matchFlowManager == null)
        {
            matchFlowManager =
                FindFirstObjectByType<MatchFlowManager>();
        }
    }

    private void RefreshReference()
    {
        FindMatchFlowManager();

        if (matchFlowManager == null)
        {
            cachedPlayer = null;
            return;
        }

        PlayerController registeredPlayer =
            matchFlowManager.GetPlayer(targetSide);

        if (registeredPlayer != null)
        {
            cachedPlayer = registeredPlayer;
        }
        else if (cachedPlayer == null)
        {
            cachedPlayer = null;
        }
    }
}
