using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>パネルの中の、1人分の「完了」表示。</summary>
[Serializable]
public class ResultReadyView
{
    public GameObject menuPrompt;   // メニューボタンのアイコン(完了するまで点滅)
    public GameObject readyCat;     // 完了したときに出る丸アイコンの猫
}

/// <summary>
/// リザルト画面の進行を管理する。
///   1. 勝敗の絵(sprite)と「1P WIN!」を表示する
///   2. 3秒後に、順位のパネルが自動で開く
///   3. パネルが開き終わってから、各プレイヤーのメニューボタンで「完了」にできる
///   4. 2人とも完了したら、暗転してタイトルへ
/// 勝者は MatchResultData から受け取る(1P = 白猫、2P = 黒猫)。
/// </summary>
public class ResultManager : MonoBehaviour
{
    private enum Phase
    {
        Entering,       // 暗転から明るくなる間
        Verdict,        // 勝敗を表示中(パネルが開くまで)
        OpeningPanel,   // パネルが開いている途中
        WaitingReady,   // メニューボタンで完了を待つ
        Leaving         // タイトルへ移行中
    }

    [Header("次のシーン")]
    [SerializeField] private string titleSceneName = "TitleScene";

    [Header("猫のスプライト(1P = 白猫、2P = 黒猫)")]
    [SerializeField] private Sprite whiteCatWin;
    [SerializeField] private Sprite whiteCatLose;
    [SerializeField] private Sprite blackCatWin;
    [SerializeField] private Sprite blackCatLose;

    [Header("勝敗の表示")]
    [Tooltip("1Pの場所(白猫)に表示する Image")]
    [SerializeField] private Image verdictImageP1;
    [Tooltip("2Pの場所(黒猫)に表示する Image")]
    [SerializeField] private Image verdictImageP2;
    [Tooltip("「1P WIN!」のように勝者を表示するテキスト")]
    [SerializeField] private Text winnerText;

    [Header("順位のパネル")]
    [SerializeField] private RectTransform panelRoot;
    [Tooltip("1位の猫を表示する Image(勝者の勝訴の絵)")]
    [SerializeField] private Image firstPlaceImage;
    [Tooltip("2位の猫を表示する Image(敗者の敗訴の絵)")]
    [SerializeField] private Image secondPlaceImage;
    [Tooltip("勝敗の表示を始めてから、パネルが自動で開くまでの時間")]
    [SerializeField] private float panelOpenDelaySeconds = 3f;
    [Tooltip("パネルが開く(拡大して現れる)のにかかる時間")]
    [SerializeField] private float panelOpenSeconds = 0.4f;
    [SerializeField] private AnimationCurve panelOpenCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("完了の表示(要素0 = 1P、要素1 = 2P)")]
    [SerializeField] private ResultReadyView[] readyViews = new ResultReadyView[2];
    [Tooltip("メニューボタンのアイコンの点滅間隔(秒)")]
    [SerializeField] private float promptBlinkSeconds = 1f;
    [Tooltip("もう一度メニューボタンを押すと完了を解除できるようにする(仕様書には記載なし)")]
    [SerializeField] private bool allowCancelReady = false;
    [Tooltip("全員が完了してから、暗転を始めるまでの間")]
    [SerializeField] private float leaveDelaySeconds = 0.8f;

    [Header("暗転")]
    [SerializeField] private ScreenFader fader;
    [SerializeField] private float openingFadeSeconds = 0.5f;
    [SerializeField] private float fadeOutSeconds = 1f;

    [Header("効果音・BGMなどの接続口")]
    [SerializeField] private UnityEvent onVerdictStart;   // 勝敗の表示が始まったとき
    [SerializeField] private UnityEvent onPanelOpened;    // パネルが開き終わったとき
    [SerializeField] private UnityEvent onAllReady;       // 2人とも完了したとき

    [Header("開発用")]
    [Tooltip("試合を経由せずにこのシーンを開いたときの勝者(1 = 1P、2 = 2P)")]
    [SerializeField] private int debugWinnerSeat = 1;
    [Tooltip("コントローラーが無い環境で、キーボードのキーを完了ボタン代わりにする")]
    [SerializeField] private bool allowKeyboard = true;
    [SerializeField] private Key debugReadyKeyP1 = Key.Space;
    [SerializeField] private Key debugReadyKeyP2 = Key.Enter;

    private Phase phase = Phase.Entering;
    private System.IDisposable anyButtonSubscription;

    private int winnerIndex;                        // 0 = 1P、1 = 2P
    private readonly bool[] ready = new bool[2];

    // =================================================================
    // Unity イベント
    // =================================================================

    private void OnEnable()
    {
        anyButtonSubscription = InputSystem.onAnyButtonPress.Call(OnAnyButtonPressed);
    }

    private void OnDisable()
    {
        anyButtonSubscription?.Dispose();
        anyButtonSubscription = null;
    }

    private void Start()
    {
        Time.timeScale = 1f;

        winnerIndex = ReadWinnerIndex();

        // 最初は、パネルと完了の表示を隠す
        if (panelRoot != null) panelRoot.gameObject.SetActive(false);

        foreach (var view in readyViews)
        {
            if (view == null) continue;
            SetActive(view.menuPrompt, false);
            SetActive(view.readyCat, false);
        }

        StartCoroutine(ResultSequence());
    }

    // 勝者のプレイヤー番号を読む(0 = 1P、1 = 2P)
    private int ReadWinnerIndex()
    {
        int number = debugWinnerSeat;

        if (MatchResultData.HasResult)
        {
            number = MatchResultData.WinnerPlayerNumber;

            if (number < 1 || number > 2)
            {
                Debug.LogWarning($"勝者のプレイヤー番号が不正です({number})。1Pの勝利として表示します。");
            }
        }

        return Mathf.Clamp(number - 1, 0, 1);
    }

    // =================================================================
    // 進行
    // =================================================================

    private IEnumerator ResultSequence()
    {
        // 黒い状態から明るくする
        if (fader != null)
        {
            fader.SetAlpha(1f);
            yield return fader.FadeIn(openingFadeSeconds);
        }

        // 勝敗の絵と文字を表示
        ShowVerdict();
        onVerdictStart.Invoke();
        phase = Phase.Verdict;

        // 時間が来たら、パネルが自動で開く
        yield return new WaitForSecondsRealtime(panelOpenDelaySeconds);

        phase = Phase.OpeningPanel;
        yield return OpenPanel();

        // パネルが開き終わってから、完了の入力を受け付ける
        onPanelOpened.Invoke();
        phase = Phase.WaitingReady;
        StartCoroutine(BlinkPrompts());
    }

    private void ShowVerdict()
    {
        // 1Pの場所には白猫、2Pの場所には黒猫。勝った方は勝訴、負けた方は敗訴の絵を入れる
        SetSprite(verdictImageP1, GetCatSprite(0, winnerIndex == 0));
        SetSprite(verdictImageP2, GetCatSprite(1, winnerIndex == 1));

        if (winnerText != null)
        {
            winnerText.text = $"{winnerIndex + 1}P WIN!";
        }
    }

    // パネルを、小さい状態から拡大して表示する
    private IEnumerator OpenPanel()
    {
        if (panelRoot == null) yield break;

        // 順位の絵を入れてから開く(1位 = 勝者の勝訴、2位 = 敗者の敗訴)
        int loserIndex = 1 - winnerIndex;
        SetSprite(firstPlaceImage, GetCatSprite(winnerIndex, true));
        SetSprite(secondPlaceImage, GetCatSprite(loserIndex, false));

        panelRoot.localScale = Vector3.zero;
        panelRoot.gameObject.SetActive(true);

        float t = 0f;
        while (t < panelOpenSeconds)
        {
            t += Time.unscaledDeltaTime;
            float p = panelOpenCurve.Evaluate(Mathf.Clamp01(t / panelOpenSeconds));
            panelRoot.localScale = Vector3.one * p;
            yield return null;
        }

        panelRoot.localScale = Vector3.one;
    }

    // メニューボタンのアイコンを点滅させる(完了していないプレイヤーだけ)
    private IEnumerator BlinkPrompts()
    {
        bool visible = true;

        while (phase == Phase.WaitingReady)
        {
            for (int i = 0; i < readyViews.Length; i++)
            {
                if (readyViews[i] == null) continue;
                SetActive(readyViews[i].menuPrompt, visible && !ready[i]);
            }

            visible = !visible;
            yield return new WaitForSecondsRealtime(promptBlinkSeconds);
        }
    }

    private void RefreshReadyUI()
    {
        for (int i = 0; i < readyViews.Length; i++)
        {
            if (readyViews[i] == null) continue;

            SetActive(readyViews[i].readyCat, ready[i]);
            if (ready[i]) SetActive(readyViews[i].menuPrompt, false);
        }
    }

    private IEnumerator LeaveToTitle()
    {
        onAllReady.Invoke();

        // 丸アイコンの猫を少し見せてから暗転する
        yield return new WaitForSecondsRealtime(leaveDelaySeconds);

        if (fader != null)
        {
            yield return fader.FadeOut(fadeOutSeconds);
        }

        SceneManager.LoadScene(titleSceneName);
    }

    // =================================================================
    // 入力(パネルが開き終わってから、完了の入力だけを受け付ける)
    // =================================================================

    private void OnAnyButtonPressed(InputControl control)
    {
        if (phase != Phase.WaitingReady) return;

        int player = ResolveReadyPlayer(control);
        if (player < 0) return;

        ready[player] = allowCancelReady ? !ready[player] : true;
        RefreshReadyUI();

        if (ready[0] && ready[1])
        {
            phase = Phase.Leaving;
            StartCoroutine(LeaveToTitle());
        }
    }

    // 「メニューボタンを押したのは誰か」(メニューボタンでなければ、または不明なら -1)
    private int ResolveReadyPlayer(InputControl control)
    {
        InputDevice device = control.device;

        if (device is Gamepad pad)
        {
            // メニューボタン以外は無視
            if (control != pad.startButton) return -1;

            // コントローラー識別で登録された席から、プレイヤーを探す
            for (int i = 0; i < GameSession.Seats.Length; i++)
            {
                var seat = GameSession.Seats[i];
                if (seat != null && seat.device == device) return i;
            }

            return -1;
        }

        // 開発用: キーボード
        if (allowKeyboard && control is KeyControl key)
        {
            if (key.keyCode == debugReadyKeyP1) return 0;
            if (key.keyCode == debugReadyKeyP2) return 1;
        }

        return -1;
    }

    // =================================================================
    // 補助
    // =================================================================

    // プレイヤー番号(0 = 1P/白猫、1 = 2P/黒猫)と勝敗から、絵を選ぶ
    private Sprite GetCatSprite(int playerIndex, bool isWinner)
    {
        if (playerIndex == 0) return isWinner ? whiteCatWin : whiteCatLose;
        return isWinner ? blackCatWin : blackCatLose;
    }

    private static void SetSprite(Image image, Sprite sprite)
    {
        if (image == null) return;

        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null) target.SetActive(active);
    }
}