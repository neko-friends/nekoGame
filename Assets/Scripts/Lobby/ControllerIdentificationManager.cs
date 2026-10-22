using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;

/// <summary>
/// コントローラー識別画面の進行管理(企画書v1.0.1「コントローラー識別画面」準拠)。
/// ・Bボタンで参加(押した順に 1P=白猫 / 2P=黒猫)
/// ・メニューボタンで準備完了のON/OFF
/// ・2人とも準備完了になったら SE → 暗転 → ロード → ゲーム画面
/// キャラ選択・CPU・トレーニング・タイトルへ戻る操作は企画書に無いので持たない。
/// </summary>
public class ControllerIdentificationManager : MonoBehaviour
{
    private class Seat
    {
        public InputDevice Device;
        public string Scheme;
        public bool Ready;
    }

    [Header("シーン")]
    [SerializeField] private string matchSceneName = "MatchScene";

    [Header("席(添字0 = 1P:白猫 / 添字1 = 2P:黒猫)")]
    [SerializeField] private PlayerSlotView[] slots = new PlayerSlotView[GameSession.SeatCount];

    [Header("試合へ渡すキャラ(今は拒否猫固定)")]
    [SerializeField] private string characterID = "Character01";

    [Header("開始演出")]
    [SerializeField] private AudioSource seSource;
    [SerializeField] private AudioClip startSe;          // 「拒否」SE
    [SerializeField] private CanvasGroup blackout;       // 全画面の黒(alpha 0 で配置)
    [SerializeField] private GameObject loadingRoot;     // ロード画面(非表示で配置)
    [SerializeField] private float fadeSeconds = 0.5f;
    [SerializeField] private float minLoadingSeconds = 1.0f;

    private readonly Seat[] seats = new Seat[GameSession.SeatCount];
    private System.IDisposable anyButtonSubscription;
    private bool isTransitioning;

    // =================================================================
    // Unity イベント
    // =================================================================

    private void OnEnable()
    {
        anyButtonSubscription = InputSystem.onAnyButtonPress.Call(OnAnyButtonPressed);
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        anyButtonSubscription?.Dispose();
        anyButtonSubscription = null;
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void Start()
    {
        for (int i = 0; i < slots.Length; i++) slots[i].ShowEmpty();

        if (blackout != null) blackout.alpha = 0f;
        if (loadingRoot != null) loadingRoot.SetActive(false);
    }

    private void Update()
    {
        if (isTransitioning) return;

        // メニューボタンで準備完了のON/OFF
        for (int i = 0; i < seats.Length; i++)
        {
            Seat seat = seats[i];
            if (seat == null || !WasMenuPressed(seat)) continue;

            seat.Ready = !seat.Ready;
            slots[i].SetReady(seat.Ready);
        }

        if (AllReady())
        {
            isTransitioning = true;
            StartCoroutine(StartMatch());
        }
    }

    // =================================================================
    // 参加(Bボタン)
    // =================================================================

    private void OnAnyButtonPressed(InputControl control)
    {
        if (isTransitioning) return;

        InputDevice device = control.device;

        if (device is Gamepad pad)
        {
            if (control == pad.buttonEast) TryJoin(device, LobbySchemes.GamepadScheme);   // Xbox の B
        }
        else if (control is KeyControl key)
        {
            // 開発用: コントローラーが無い環境ではキーボードの「決定」キーを B の代わりにする
            if (key.keyCode == LobbySchemes.Keyboard1Keys.confirm) TryJoin(device, LobbySchemes.Keyboard1Scheme);
            else if (key.keyCode == LobbySchemes.Keyboard2Keys.confirm) TryJoin(device, LobbySchemes.Keyboard2Scheme);
        }
    }

    private void TryJoin(InputDevice device, string scheme)
    {
        // 同じコントローラーの二重参加は不可
        foreach (var s in seats)
        {
            if (s != null && s.Device == device && s.Scheme == scheme) return;
        }

        // 押された順に空いている席へ(1P=白が先)
        int index = System.Array.IndexOf(seats, null);
        if (index < 0) return;

        seats[index] = new Seat { Device = device, Scheme = scheme, Ready = false };
        slots[index].ShowJoined(device, scheme);
    }

    // コントローラーが抜けたら、その席を空ける(企画書に記述なし。仮仕様)
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (isTransitioning) return;
        if (change != InputDeviceChange.Disconnected && change != InputDeviceChange.Removed) return;

        for (int i = 0; i < seats.Length; i++)
        {
            if (seats[i] != null && seats[i].Device == device)
            {
                seats[i] = null;
                slots[i].ShowEmpty();
            }
        }
    }

    // =================================================================
    // 準備完了 / 開始
    // =================================================================

    private static bool WasMenuPressed(Seat seat)
    {
        if (seat.Device is Gamepad pad) return pad.startButton.wasPressedThisFrame;   // Xbox のメニューボタン
        if (seat.Device is Keyboard kb) return kb[LobbySchemes.GetKeys(seat.Scheme).start].wasPressedThisFrame;
        return false;
    }

    // 2席とも埋まっていて、2人とも準備完了
    private bool AllReady()
    {
        foreach (var s in seats)
        {
            if (s == null || !s.Ready) return false;
        }
        return true;
    }

    private IEnumerator StartMatch()
    {
        WriteSession();

        if (seSource != null && startSe != null) seSource.PlayOneShot(startSe);

        // 暗転
        if (blackout != null)
        {
            blackout.gameObject.SetActive(true);
            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                blackout.alpha = Mathf.Clamp01(t / fadeSeconds);
                yield return null;
            }
            blackout.alpha = 1f;
        }

        // ロード画面を挟んでから試合シーンへ
        if (loadingRoot != null) loadingRoot.SetActive(true);

        AsyncOperation op = SceneManager.LoadSceneAsync(matchSceneName);
        op.allowSceneActivation = false;

        float elapsed = 0f;
        while (elapsed < minLoadingSeconds || op.progress < 0.9f)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        op.allowSceneActivation = true;
    }

    // 席の情報を GameSession に書き込む(席番号 = 押された順 = 色)
    private void WriteSession()
    {
        for (int i = 0; i < seats.Length; i++)
        {
            GameSession.Seats[i] = new PlayerSessionData
            {
                playerNumber = i + 1,
                controlType = PlayerControlType.Human,
                characterID = characterID,
                schemeName = seats[i].Scheme,
                device = seats[i].Device
            };
        }
    }
}
