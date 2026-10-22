using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 技の「文字イラスト」と「音声」を出す表示担当。猫のルート(PlayerController と同じオブジェクト)に付ける。
/// ・技が出す文字(拒否 / 抵抗 / 逃げたい / 寝る / がんばえ ...)と同じ名前の項目に、イラストと音声を設定する
/// ・イラスト(Sprite)が未設定の項目は、仮の文字(TextMesh)で表示する
/// ・音声が未設定なら無音
/// 表示位置は「猫の頭の上」(体の当たりの上端 + offsetAboveHead)。HP表示のさらに上になるよう調整できる。
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class SkillCueView : MonoBehaviour
{
    [Serializable]
    private class CueEntry
    {
        public string text;        // 例: 拒否 / 抵抗 / 逃げたい / 寝る / がんばえ
        public Sprite sprite;      // 文字のイラスト
        public AudioClip clip;     // 音声
    }

    [Header("文字イラストと音声(文字ごと)")]
    [FormerlySerializedAs("voices")]
    [SerializeField] private CueEntry[] cues;

    [Header("表示")]
    [Tooltip("頭の上端からの距離(ワールド座標)。HP表示と重なるときは大きくする")]
    [SerializeField] private float offsetAboveHead = 0.8f;
    [SerializeField] private float displaySeconds = 1f;
    [SerializeField] private float spriteScale = 1f;
    [SerializeField] private int sortingOrder = 100;

    [Header("仮の文字(イラスト未設定のとき)")]
    [SerializeField] private float characterSize = 0.1f;
    [SerializeField] private int fontSize = 64;
    [SerializeField] private Color textColor = new Color(1f, 0.9f, 0.2f);

    private PlayerController owner;
    private AudioSource audioSource;
    private Font font;

    private void Awake()
    {
        owner = GetComponent<PlayerController>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        owner.SkillCue += OnCue;
    }

    private void OnDestroy()
    {
        if (owner != null) owner.SkillCue -= OnCue;
    }

    private void OnCue(string text)
    {
        CueEntry entry = FindEntry(text);

        Show(text, entry);

        if (entry != null && entry.clip != null)
        {
            audioSource.PlayOneShot(entry.clip);
        }
    }

    private CueEntry FindEntry(string text)
    {
        if (cues == null) return null;

        foreach (CueEntry e in cues)
        {
            if (e != null && e.text == text) return e;
        }
        return null;
    }

    private void Show(string text, CueEntry entry)
    {
        var go = new GameObject("SkillCue_" + text);
        go.transform.SetParent(transform, false);

        // 頭の上端の少し上(猫の中心を基準にした高さ)
        float topY = owner.BodyBounds.max.y - transform.position.y;
        go.transform.localPosition = new Vector3(0f, topY + offsetAboveHead, 0f);

        if (entry != null && entry.sprite != null)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = entry.sprite;
            sr.sortingOrder = sortingOrder;
            go.transform.localScale = Vector3.one * spriteScale;
        }
        else
        {
            ShowFallbackText(go, text);
        }

        Destroy(go, displaySeconds);
    }

    // イラストが無いときの仮の文字(OSの日本語フォントを使う)
    private void ShowFallbackText(GameObject go, string text)
    {
        if (font == null)
        {
            font = Font.CreateDynamicFontFromOSFont(
                new[] { "Yu Gothic", "Meiryo", "Hiragino Sans", "Hiragino Kaku Gothic ProN", "Noto Sans CJK JP" },
                fontSize);
        }

        var mesh = go.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.font = font;
        mesh.fontSize = fontSize;
        mesh.characterSize = characterSize;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = textColor;

        var renderer = go.GetComponent<MeshRenderer>();
        renderer.material = font.material;
        renderer.sortingOrder = sortingOrder;
    }
}