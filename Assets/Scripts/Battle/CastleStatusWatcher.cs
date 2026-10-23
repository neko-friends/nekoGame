using UnityEngine;

/// <summary>城オブジェクトの消失を検知して、相手陣営の勝利を通知する。</summary>
public class CastleStatusWatcher : MonoBehaviour
{
    [SerializeField] private TeamSide ownerSide = TeamSide.Left;
    [SerializeField] private GameObject castleObject;
    [SerializeField] private MatchFlowManager matchFlowManager;

    private bool hasObservedActive;
    private bool hasReported;

    private void Awake()
    {
        if (castleObject == null) castleObject = gameObject;
        if (matchFlowManager == null) matchFlowManager = FindFirstObjectByType<MatchFlowManager>();
    }

    private void Update()
    {
        if (hasReported) return;
        if (castleObject == null)
        {
            if (hasObservedActive) ReportDestroyed();
            return;
        }
        if (castleObject.activeInHierarchy)
        {
            hasObservedActive = true;
            return;
        }
        if (hasObservedActive) ReportDestroyed();
    }

    // このコンポーネント自身が城に付いている場合、城の非アクティブ化・破棄でUpdateが止まるため、ここでも検知する。
    private void OnDisable()
    {
        if (hasObservedActive && !hasReported) ReportDestroyed();
    }

    private void OnDestroy()
    {
        if (hasObservedActive && !hasReported) ReportDestroyed();
    }

    /// <summary>城が残る仕様の場合、破壊成立時に城側の処理から呼び出す。</summary>
    public void ReportDestroyed()
    {
        if (hasReported) return;
        hasReported = true;
        if (matchFlowManager == null)
        {
            Debug.LogError("CastleStatusWatcher：MatchFlowManagerが見つかりません。", this);
            return;
        }
        TeamSide winner = ownerSide == TeamSide.Left ? TeamSide.Right : TeamSide.Left;
        matchFlowManager.NotifyCastleDestroyed(winner);
    }
}
