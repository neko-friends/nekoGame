using UnityEngine;
using TMPro;

/// <summary>制限時間を表示し、時間切れを試合管理へ通知する。</summary>
public class GameTimer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField, Header("制限時間（秒）"), Min(0f)] private float startTime = 60f;
    [SerializeField, Header("試合進行管理")] private MatchFlowManager matchFlowManager;

    private float currentTime;
    private bool hasEnded;

    public float CurrentTime => currentTime;
    public bool HasEnded => hasEnded;

    private void Start()
    {
        currentTime = Mathf.Max(0f, startTime);
        UpdateTimerText();
        if (currentTime <= 0f) EndTimer();
    }

    private void Update()
    {
        if (hasEnded) return;
        currentTime = Mathf.Max(0f, currentTime - Time.deltaTime);
        UpdateTimerText();
        if (currentTime <= 0f) EndTimer();
    }

    private void EndTimer()
    {
        if (hasEnded) return;
        hasEnded = true;
        currentTime = 0f;
        UpdateTimerText();

        if (matchFlowManager == null)
        {
            Debug.LogError("GameTimer：MatchFlowManagerが設定されていません。", this);
            return;
        }
        matchFlowManager.NotifyTimeExpired();
    }

    /// <summary>試合終了時にタイマーを停止する。時間切れ通知は行わない。</summary>
    public void StopTimer()
    {
        hasEnded = true;
    }

    private void UpdateTimerText()
    {
        if (timerText == null)
        {
            Debug.LogWarning("GameTimer：Timer Textが設定されていません。", this);
            return;
        }
        int minutes = Mathf.FloorToInt(currentTime / 60f);
        int seconds = Mathf.FloorToInt(currentTime % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";
    }
}
