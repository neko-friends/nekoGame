using System;
using UnityEngine;

/// <summary>
/// 技の「文字」と「音声」を出す表示担当。猫のルート(PlayerController と同じオブジェクト)に付ける。
/// ・文字は猫の頭上に一定時間表示する(仮の表示。TextMesh を使用)
/// ・音声は「文字」と同じ名前の項目に AudioClip を設定しておくと、表示と同時に鳴る(未設定なら無音)
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class SkillCueView : MonoBehaviour
{
    [Serializable]
    private class Voice
    {
        public string text;        // 例: 拒否 / 抵抗 / 逃げたい / 寝る / がんばえ
        public AudioClip clip;
    }

    [Header("音声(文字ごと)")]
    [SerializeField] private Voice[] voices;

    [Header("文字の見た目")]
    [SerializeField] private Vector3 offsetFromCenter = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private float displaySeconds = 1f;
    [SerializeField] private float characterSize = 0.1f;
    [SerializeField] private int fontSize = 64;
    [SerializeField] private Color textColor = new Color(1f, 0.9f, 0.2f);
    [SerializeField] private int sortingOrder = 100;

    private PlayerController owner;
    private AudioSource audioSource;
    private Font font;

    private void Awake()
    {
        owner = GetComponent<PlayerController>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // 日本語が出せるよう、OSのフォントを使う(仮。後で専用フォントに差し替える)
        font = Font.CreateDynamicFontFromOSFont(
            new[] { "Yu Gothic", "Meiryo", "Hiragino Sans", "Hiragino Kaku Gothic ProN", "Noto Sans CJK JP" },
            fontSize);

        owner.SkillCue += OnCue;
    }

    private void OnDestroy()
    {
        if (owner != null) owner.SkillCue -= OnCue;
    }

    private void OnCue(string text)
    {
        ShowText(text);
        PlayVoice(text);
    }

    private void ShowText(string text)
    {
        var go = new GameObject("SkillCueText");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = offsetFromCenter;

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

        Destroy(go, displaySeconds);
    }

    private void PlayVoice(string text)
    {
        if (voices == null) return;

        foreach (Voice v in voices)
        {
            if (v.text == text && v.clip != null)
            {
                audioSource.PlayOneShot(v.clip);
                return;
            }
        }
    }
}
