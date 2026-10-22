using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>必殺技の種類(入力方向に対応)。</summary>
public enum SkillSlot { Neutral, Side, Up, Down }

/// <summary>
/// 企画書の「画像」と「フレーム」の対応。
/// 現在の解釈: 画像n = (n-1)×6+1 〜 n×6 フレーム(6フレームごとに画像が切り替わる。全30フレーム)
/// ※ 企画書の★の読み方を変えるときは、この2つのメソッドだけを直す。
/// </summary>
public static class SkillTiming
{
    public const int FramesPerImage = 6;

    public static int ImageStart(int image) => (image - 1) * FramesPerImage + 1;
    public static int ImageEnd(int image) => image * FramesPerImage;
}

/// <summary>当たり判定が出ている期間。位置と大きさは「猫の体の大きさ」を1とした倍率(右向きのとき)。</summary>
[Serializable]
public class SkillHitWindow
{
    public int startFrame;
    public int endFrame;
    public Vector2 offset;   // 猫の中心からのずれ(横は向きに合わせて反転)
    public Vector2 size;     // 判定の大きさ
    public int group;        // 同じグループは、同じ相手に1回しか当たらない
}

/// <summary>指定フレームに頭上へ文字(と音声)を出す。</summary>
[Serializable]
public class SkillCue
{
    public int frame;
    public string text;
}

/// <summary>1つの必殺技の内容。「何フレーム目に何をするか」をデータで持つ。</summary>
[Serializable]
public class SkillData
{
    public string name;
    public SkillSlot slot;
    public int totalFrames;

    public List<SkillHitWindow> hits = new List<SkillHitWindow>();
    public List<SkillCue> cues = new List<SkillCue>();

    // 上昇(riseBodyHeights が 0 なら上昇しない)
    public float riseBodyHeights;   // 猫の体の高さの何倍上がるか
    public int riseStartFrame;
    public int riseEndFrame;        // このフレームまでに上がりきる
}

/// <summary>
/// キャラごとの必殺技の一覧。現在は拒否猫(unit0001)だけ実装している。
/// 【仮】と書いた数値は企画書に記載がないため、見た目を見ながら調整してよい。
/// </summary>
public static class SkillLibrary
{
    public const string DefaultCharacterId = "unit0001";   // 拒否猫

    public static SkillData Get(string characterId, SkillSlot slot)
    {
        if (string.IsNullOrEmpty(characterId)) characterId = DefaultCharacterId;

        if (characterId != DefaultCharacterId)
        {
            Debug.LogWarning($"必殺技は未実装のキャラです: {characterId}");
            return null;
        }

        switch (slot)
        {
            case SkillSlot.Neutral: return KyohiNekoNeutral();
            case SkillSlot.Side: return KyohiNekoSide();
            case SkillSlot.Up: return KyohiNekoUp();
            case SkillSlot.Down: return KyohiNekoDown();
            default: return null;
        }
    }

    // N必殺: 画像4から集中線のエフェクト(判定)を猫の中心に出し、画像5で猫より一回り大きくする
    private static SkillData KyohiNekoNeutral()
    {
        var s = new SkillData { name = "N必殺", slot = SkillSlot.Neutral, totalFrames = 30 };

        // 【仮】画像4は猫と同じ大きさ、画像5は1.5倍(「一回り大きい」の解釈)
        s.hits.Add(new SkillHitWindow
        {
            startFrame = SkillTiming.ImageStart(4), endFrame = SkillTiming.ImageEnd(4),
            offset = Vector2.zero, size = new Vector2(1f, 1f), group = 0
        });
        s.hits.Add(new SkillHitWindow
        {
            startFrame = SkillTiming.ImageStart(5), endFrame = SkillTiming.ImageEnd(5),
            offset = Vector2.zero, size = new Vector2(1.5f, 1.5f), group = 0
        });

        s.cues.Add(new SkillCue { frame = 24, text = "拒否" });
        return s;
    }

    // 左右必殺: 画像2から向いている方向に連続のこぶし。画像2と3を同じ間隔で5回繰り返す
    private static SkillData KyohiNekoSide()
    {
        const int repeats = 5;
        int cycle = SkillTiming.FramesPerImage * 2;   // 画像2+3 の12フレーム

        // 【仮】全体の長さ = 準備(画像1)+ 12フレーム × 5回 = 66フレーム
        var s = new SkillData
        {
            name = "左右必殺",
            slot = SkillSlot.Side,
            totalFrames = SkillTiming.ImageEnd(1) + cycle * repeats
        };

        for (int i = 0; i < repeats; i++)
        {
            int start = SkillTiming.ImageStart(2) + cycle * i;

            // 【仮】こぶしの位置と大きさ。1回ごとに別のグループ(それぞれ1回ずつ当たる)
            s.hits.Add(new SkillHitWindow
            {
                startFrame = start, endFrame = start + cycle - 1,
                offset = new Vector2(1f, 0f), size = new Vector2(1.2f, 0.5f), group = i + 1
            });
        }

        s.cues.Add(new SkillCue { frame = 12, text = "抵抗" });
        return s;
    }

    // 上必殺: 画像3から判定と上昇を始め、画像5の時には猫3体分上にいる。判定は猫全体
    private static SkillData KyohiNekoUp()
    {
        var s = new SkillData { name = "上必殺", slot = SkillSlot.Up, totalFrames = 30 };

        s.hits.Add(new SkillHitWindow
        {
            startFrame = SkillTiming.ImageStart(3), endFrame = 30,
            offset = Vector2.zero, size = new Vector2(1f, 1f), group = 0
        });

        s.riseBodyHeights = 3f;
        s.riseStartFrame = SkillTiming.ImageStart(3);
        s.riseEndFrame = SkillTiming.ImageEnd(4);   // 画像5に入るまでに上がりきる

        s.cues.Add(new SkillCue { frame = 18, text = "逃げたい" });
        return s;
    }

    // 下必殺: 画像4から判定を出す。判定は猫全体
    private static SkillData KyohiNekoDown()
    {
        var s = new SkillData { name = "下必殺", slot = SkillSlot.Down, totalFrames = 30 };

        s.hits.Add(new SkillHitWindow
        {
            startFrame = SkillTiming.ImageStart(4), endFrame = 30,
            offset = Vector2.zero, size = new Vector2(1f, 1f), group = 0
        });

        s.cues.Add(new SkillCue { frame = 24, text = "寝る" });
        return s;
    }
}
