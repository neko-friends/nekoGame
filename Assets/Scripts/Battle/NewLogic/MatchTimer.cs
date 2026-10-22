using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 試合の残り時間(企画書のゲーム画面上部の「3:34」表示)。
/// 0になったら MatchFlowManager.NotifyTimeExpired() を呼ぶ。
/// 制限時間の長さは企画書に記載がないため仮の値。
/// </summary>
public class MatchTimer : MonoBehaviour
{
    [SerializeField] private MatchFlowManager matchFlow;
    [SerializeField] private Text timerText;

    [Tooltip("試合時間(秒)。企画書に記載なし。仮")]
    [SerializeField, Min(1f)] private float matchSeconds = 240f;

    [Tooltip("オンのとき、シーン開始と同時にカウントを始める")]
    [SerializeField] private bool startOnStart = true;

    private float remaining;
    private bool running;

    public float RemainingSeconds => remaining;

    private void Start()
    {
        remaining = matchSeconds;
        Refresh();
        running = startOnStart;
    }

    private void Update()
    {
        if (!running) return;

        remaining -= Time.deltaTime;

        if (remaining <= 0f)
        {
            remaining = 0f;
            running = false;
            Refresh();

            if (matchFlow != null) matchFlow.NotifyTimeExpired();
            return;
        }

        Refresh();
    }

    public void StartTimer()
    {
        running = true;
    }

    public void StopTimer()
    {
        running = false;
    }

    private void Refresh()
    {
        if (timerText == null) return;

        int total = Mathf.CeilToInt(remaining);
        timerText.text = $"{total / 60}:{total % 60:00}";
    }
}
