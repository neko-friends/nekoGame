using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;

/// <summary>
/// タイトル画面の進行を管理する。仕様書どおりの流れ:
///   1. タイトルロゴを3秒かけてフェードイン
///   2. ロゴが表示されたら PUSH START BUTTON を点滅させ、ボタン入力を待つ
///   3. ボタンが押されたら、遷移の演出(妖怪ウォッチ風)→ 画面をフェードアウト
///   4. コントローラー識別画面へ移動
/// </summary>
public class TitleManager : MonoBehaviour
{
    private enum Phase
    {
        LogoFadeIn,     // ロゴが出てくるまで(入力は受け付けない)
        WaitingInput,   // PUSH START BUTTON の点滅中(入力待ち)
        Transition      // 押された後の演出〜画面遷移
    }

    [Header("次のシーン")]
    [Tooltip("コントローラー識別画面のシーン名(Build Profiles に登録しておく)")]
    [SerializeField] private string nextSceneName = "ControllerSelectScene";

    [Header("タイトルロゴ")]
    [SerializeField] private CanvasGroup logoGroup;
    [SerializeField] private float logoFadeInSeconds = 3f;

    [Header("PUSH START BUTTON")]
    [SerializeField] private CanvasGroup pushStartGroup;
    [Tooltip("点滅の間隔(秒)。仕様書では5秒。短くしたい場合はここを変える")]
    [SerializeField] private float blinkIntervalSeconds = 5f;

    [Header("暗転")]
    [SerializeField] private ScreenFader fader;
    [Tooltip("シーン開始時に、黒から明るくなる時間")]
    [SerializeField] private float openingFadeSeconds = 0.5f;
    [Tooltip("遷移の演出のあと、画面が暗くなる時間")]
    [SerializeField] private float fadeOutSeconds = 1f;

    [Header("遷移の演出(妖怪ウォッチ風・仮)")]
    [Tooltip("Animator を使う場合: トリガーを引いて演出を再生する(未設定なら下の仮演出)")]
    [SerializeField] private Animator transitionAnimator;
    [SerializeField] private string transitionTrigger = "Start";
    [Tooltip("仮演出で動かす対象(ロゴなど)")]
    [SerializeField] private RectTransform transitionTarget;
    [SerializeField] private float transitionSeconds = 1.2f;
    [SerializeField] private float zoomScale = 4f;
    [SerializeField] private float spinDegrees = 360f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("効果音・BGMなどの接続口")]
    [SerializeField] private UnityEvent onLogoShown;   // ロゴが表示されたとき
    [SerializeField] private UnityEvent onPushStart;   // ボタンが押されたとき

    [Header("開発用")]
    [Tooltip("コントローラーが無い環境で、キーボードでも進められるようにする")]
    [SerializeField] private bool allowKeyboard = true;

    private Phase phase = Phase.LogoFadeIn;
    private System.IDisposable anyButtonSubscription;

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
        // タイトルはゲームの出発点。前回の席・モードなどをすべて初期化する
        GameSession.ResetSession();
        MatchResultData.Reset();   // 前回の試合結果を消す(消さないと次の試合の結果が上書きできない)
        Time.timeScale = 1f;

        if (logoGroup != null) logoGroup.alpha = 0f;
        if (pushStartGroup != null) pushStartGroup.alpha = 0f;

        StartCoroutine(TitleSequence());
    }

    // =================================================================
    // 入力
    // =================================================================

    private void OnAnyButtonPressed(InputControl control)
    {
        if (phase != Phase.WaitingInput) return;

        InputDevice device = control.device;
        bool accepted = device is Gamepad || (allowKeyboard && device is Keyboard);
        if (!accepted) return;

        phase = Phase.Transition;
        StartCoroutine(PushStartSequence());
    }

    // =================================================================
    // 進行
    // =================================================================

    // 開始 → ロゴのフェードイン → 点滅しながら入力待ち
    private IEnumerator TitleSequence()
    {
        // 黒い状態から明るくする
        if (fader != null)
        {
            fader.SetAlpha(1f);
            yield return fader.FadeIn(openingFadeSeconds);
        }

        // ロゴを3秒かけてフェードイン(この間はボタンを受け付けない)
        yield return FadeGroup(logoGroup, 0f, 1f, logoFadeInSeconds);

        onLogoShown.Invoke();
        phase = Phase.WaitingInput;

        // PUSH START BUTTON を点滅させる
        bool visible = true;
        SetAlpha(pushStartGroup, 1f);

        float timer = 0f;
        while (phase == Phase.WaitingInput)
        {
            timer += Time.unscaledDeltaTime;

            if (timer >= blinkIntervalSeconds)
            {
                timer = 0f;
                visible = !visible;
                SetAlpha(pushStartGroup, visible ? 1f : 0f);
            }

            yield return null;
        }
    }

    // ボタンが押された → 演出 → 暗転 → 次のシーン
    private IEnumerator PushStartSequence()
    {
        onPushStart.Invoke();

        // 押したあとは PUSH START を表示したままにする
        SetAlpha(pushStartGroup, 1f);

        yield return PlayTransition();

        if (fader != null)
        {
            yield return fader.FadeOut(fadeOutSeconds);
        }

        SceneManager.LoadScene(nextSceneName);
    }

    // 遷移の演出。Animator があればそれを再生、なければ仮演出(拡大しながら回転)
    private IEnumerator PlayTransition()
    {
        if (transitionAnimator != null && !string.IsNullOrEmpty(transitionTrigger))
        {
            transitionAnimator.SetTrigger(transitionTrigger);
            yield return new WaitForSecondsRealtime(transitionSeconds);
            yield break;
        }

        if (transitionTarget == null)
        {
            yield return new WaitForSecondsRealtime(transitionSeconds);
            yield break;
        }

        Vector3 startScale = transitionTarget.localScale;
        Quaternion startRotation = transitionTarget.localRotation;

        float t = 0f;
        while (t < transitionSeconds)
        {
            t += Time.unscaledDeltaTime;
            float p = transitionCurve.Evaluate(Mathf.Clamp01(t / transitionSeconds));

            transitionTarget.localScale = Vector3.LerpUnclamped(startScale, startScale * zoomScale, p);
            transitionTarget.localRotation = startRotation * Quaternion.Euler(0f, 0f, -spinDegrees * p);

            yield return null;
        }
    }

    // =================================================================
    // 補助
    // =================================================================

    private static IEnumerator FadeGroup(CanvasGroup group, float from, float to, float seconds)
    {
        if (group == null)
        {
            yield return new WaitForSecondsRealtime(seconds);
            yield break;
        }

        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }

        group.alpha = to;
    }

    private static void SetAlpha(CanvasGroup group, float alpha)
    {
        if (group != null) group.alpha = alpha;
    }
}