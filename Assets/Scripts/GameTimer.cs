
using UnityEngine;
using TMPro;

public class GameTimer : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI timerText;

    [SerializeField, Header("制限時間（秒）")]
    private float startTime = 60f;

    [SerializeField, Header("試合進行管理")]
    private MatchFlowManager matchFlowManager;

    private float currentTime;
    private bool hasEnded;

    void Start()
    {
        currentTime = Mathf.Max(0f, startTime);
        UpdateTimerText();

        // 制限時間が最初から0秒の場合にも終了処理を行う。
        if (currentTime <= 0f)
        {
            EndTimer();
        }
    }

    void Update()
    {
        if (hasEnded)
        {
            return;
        }

        if (currentTime > 0f)
        {
            currentTime -= Time.deltaTime;

            if (currentTime < 0f)
            {
                currentTime = 0f;
            }

            UpdateTimerText();

            if (currentTime <= 0f)
            {
                EndTimer();
            }
        }
    }

    private void EndTimer()
    {
        if (hasEnded)
        {
            return;
        }

        hasEnded = true;
        currentTime = 0f;
        UpdateTimerText();

        if (matchFlowManager == null)
        {
            Debug.LogError(
                "GameTimer: MatchFlowManagerが設定されていません。",
                this
            );
            return;
        }

        matchFlowManager.NotifyTimeExpired();
    }

    private void UpdateTimerText()
    {
        if (timerText == null)
        {
            Debug.LogWarning(
                "GameTimer: Timer Textが設定されていません。",
                this
            );
            return;
        }

        int minutes = Mathf.FloorToInt(currentTime / 60f);
        int seconds = Mathf.FloorToInt(currentTime % 60f);

        timerText.text = string.Format(
            "{0:00}:{1:00}",
            minutes,
            seconds
        );
    }
}
