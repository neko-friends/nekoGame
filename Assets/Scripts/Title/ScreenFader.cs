using System.Collections;
using UnityEngine;

/// <summary>
/// 画面全体の暗転(フェード)用コンポーネント。
/// 黒い Image(全画面)と CanvasGroup が付いたオブジェクトに付ける。
/// タイトル、コントローラー識別、ゲーム、リザルトなど、暗転が必要な画面で共通で使える。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    private CanvasGroup group;

    private CanvasGroup Group
    {
        get
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            return group;
        }
    }

    /// <summary>現在の黒さ。0 = 透明、1 = 真っ暗。</summary>
    public float Alpha => Group.alpha;

    /// <summary>黒さを即座に設定する。</summary>
    public void SetAlpha(float alpha)
    {
        Group.alpha = Mathf.Clamp01(alpha);

        // 暗転中は、背面のUIをクリックされないようにする
        Group.blocksRaycasts = Group.alpha > 0.01f;
        Group.interactable = false;
    }

    /// <summary>指定の黒さまで、seconds 秒かけて変化させる。</summary>
    public IEnumerator FadeTo(float target, float seconds)
    {
        float start = Group.alpha;

        if (seconds <= 0f)
        {
            SetAlpha(target);
            yield break;
        }

        float t = 0f;

        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;   // 時間停止中でも動くように unscaled を使う
            SetAlpha(Mathf.Lerp(start, target, t / seconds));
            yield return null;
        }

        SetAlpha(target);
    }

    /// <summary>暗転する(画面が黒くなる)。</summary>
    public IEnumerator FadeOut(float seconds)
    {
        return FadeTo(1f, seconds);
    }

    /// <summary>暗転を解除する(黒から元の画面に戻る)。</summary>
    public IEnumerator FadeIn(float seconds)
    {
        return FadeTo(0f, seconds);
    }
}
