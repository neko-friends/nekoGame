using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 識別画面の中の四角形の中で動く猫に実装してもらう口。
/// 参加したコントローラーを渡すので、移動・攻撃の入力はそのデバイスから読む。
/// (PlayerInput.Instantiate(prefab, pairWithDevice: device) を使う実装でもOK)
/// </summary>
public interface ILobbyCat
{
    void Bind(InputDevice device, string scheme);
    void Unbind();
}

/// <summary>
/// 席1つ分の表示(企画書v1.0.1「コントローラー識別画面」)。
/// ・未参加: Bボタンのアイコンを点滅
/// ・参加後: 拒否猫(1P白 / 2P黒)を表示し、メニューボタンを点滅
/// ・準備完了: ○アイコンの拒否猫を表示
/// 白/黒の違いは、席ごとに別の猫オブジェクトをシーンに置いて表現する。
/// </summary>
public class PlayerSlotView : MonoBehaviour
{
    [SerializeField] private GameObject joinIcon;    // Bボタンのアイコン(未参加時に点滅)
    [SerializeField] private GameObject catRoot;     // 拒否猫(参加で表示)
    [SerializeField] private GameObject menuHint;    // メニューボタンの表示(参加後に点滅)
    [SerializeField] private GameObject readyIcon;   // ○アイコンの拒否猫(準備完了で表示)

    [Tooltip("点滅の切り替え間隔(秒)。表示1秒→非表示1秒の繰り返し")]
    [SerializeField] private float blinkInterval = 1f;

    private bool joined;
    private float blinkStart;
    private ILobbyCat cat;

    private void Update()
    {
        // 点滅は Time.unscaledTime 基準(Time.timeScale の影響を受けない)
        bool on = (int)((Time.unscaledTime - blinkStart) / blinkInterval) % 2 == 0;

        if (joined) SetActiveIfChanged(menuHint, on);
        else SetActiveIfChanged(joinIcon, on);
    }

    public void ShowEmpty()
    {
        if (joined && cat != null) cat.Unbind();

        joined = false;
        blinkStart = Time.unscaledTime;

        SetActiveIfChanged(joinIcon, true);
        SetActiveIfChanged(catRoot, false);
        SetActiveIfChanged(menuHint, false);
        SetActiveIfChanged(readyIcon, false);
    }

    public void ShowJoined(InputDevice device, string scheme)
    {
        joined = true;
        blinkStart = Time.unscaledTime;

        SetActiveIfChanged(joinIcon, false);
        SetActiveIfChanged(catRoot, true);
        SetActiveIfChanged(menuHint, true);
        SetActiveIfChanged(readyIcon, false);

        // 猫は表示してから(非アクティブでも探せるよう true を渡して)バインドする
        cat = catRoot != null ? catRoot.GetComponentInChildren<ILobbyCat>(true) : null;
        if (cat != null) cat.Bind(device, scheme);
    }

    public void SetReady(bool ready)
    {
        SetActiveIfChanged(readyIcon, ready);
    }

    private static void SetActiveIfChanged(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active) go.SetActive(active);
    }
}
