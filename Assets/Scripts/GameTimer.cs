using UnityEngine;
using TMPro;

public class GameTimer : MonoBehaviour
{
    [SerializeField] 
    private TextMeshProUGUI timerText;
    [SerializeField,Header("タイマー:分単位でも秒で入力")]
    private float startTime = 60f;//分を秒で書き込む

    private float currentTime;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentTime = startTime;
        UpdateTimerText();
    }

    // Update is called once per frame
    void Update()
    {
        if(currentTime>0)
        {
            currentTime -= Time.deltaTime;
            if(currentTime < 0 )
            {
                currentTime = 0;
            }
            UpdateTimerText();
        }
    }
    
    void UpdateTimerText()
    {
        int minutes = Mathf.FloorToInt(currentTime / 60);
        int seconds = Mathf.FloorToInt(currentTime % 60);

        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
