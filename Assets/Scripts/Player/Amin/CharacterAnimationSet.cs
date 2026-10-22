using System;
using UnityEngine;

/// <summary>
/// 1つの動きの画像の並び。ゲームのフレーム(FixedUpdate)に合わせて画像を切り替える。
/// ・Sprites に、画像を順番に並べる(1枚ずつ別のPNGをそのまま入れてよい)
/// ・Frames Per Sprite: 1枚を何フレーム見せるか(企画書の技は6フレーム)
/// ・Sequence: 空なら 1,2,3...の順。順番を変えたいときだけ、画像の番号(1始まり)を並べる
///     例) 左右必殺 = 1,2,3,2,3,2,3,2,3,2,3 / 最初の画像だけ長くしたい = 1,1,2,3,4
/// ・Loop: ON なら最後まで行ったら最初に戻る。OFF なら最後の画像で止まる
/// </summary>
[Serializable]
public class SpriteClip
{
    public Sprite[] sprites;
    [Min(1)] public int framesPerSprite = 6;
    public int[] sequence;
    public bool loop;

    public bool IsEmpty => sprites == null || sprites.Length == 0;

    /// <summary>全体のフレーム数(Sequence を使うときはその長さ × Frames Per Sprite)。</summary>
    public int TotalFrames
    {
        get
        {
            int steps = (sequence != null && sequence.Length > 0) ? sequence.Length : (sprites != null ? sprites.Length : 0);
            return steps * Mathf.Max(1, framesPerSprite);
        }
    }

    /// <summary>frame(1始まり)に表示する画像。範囲外は最後の画像(Loop のときは最初へ戻る)。</summary>
    public Sprite GetSpriteAtFrame(int frame)
    {
        if (IsEmpty) return null;

        bool useSequence = sequence != null && sequence.Length > 0;
        int steps = useSequence ? sequence.Length : sprites.Length;

        int step = Mathf.Max(0, frame - 1) / Mathf.Max(1, framesPerSprite);
        step = loop ? step % steps : Mathf.Min(step, steps - 1);

        int index = useSequence ? sequence[step] - 1 : step;
        index = Mathf.Clamp(index, 0, sprites.Length - 1);

        return sprites[index];
    }
}

/// <summary>
/// キャラ1体分のアニメーションの設定。状態ごとに画像を並べる。
/// 画像を入れていない動きは「いまの絵のまま」になる(少しずつ追加できる)。
/// </summary>
[CreateAssetMenu(fileName = "CharacterAnimationSet", menuName = "拒否猫戦争/Character Animation Set")]
public class CharacterAnimationSet : ScriptableObject
{
    public string characterID = "unit0001";

    [Header("基本の動き(繰り返しは Loop を ON)")]
    public SpriteClip idle = new SpriteClip { loop = true };
    public SpriteClip run = new SpriteClip { loop = true };
    public SpriteClip jumpUp = new SpriteClip();     // 上昇中
    public SpriteClip fall = new SpriteClip();       // 落下中

    [Header("ガード・被弾")]
    public SpriteClip guard = new SpriteClip();
    public SpriteClip guardBreak = new SpriteClip(); // スタン
    public SpriteClip hitstun = new SpriteClip();    // のけぞり
    public SpriteClip dead = new SpriteClip();       // 撃破

    [Header("攻撃(企画書: 画像1枚=6フレーム)")]
    public SpriteClip normalAttack = new SpriteClip();
    public SpriteClip neutralSpecial = new SpriteClip();
    public SpriteClip sideSpecial = new SpriteClip();
    public SpriteClip upSpecial = new SpriteClip();
    public SpriteClip downSpecial = new SpriteClip();
}
