using System;
using UnityEngine;

/// <summary>
/// 技が出す「画像オブジェクト」(拳など)1種類分の設定。
/// ・Sprites: 画像を順番に並べる(例: 画像2の拳、画像3の拳)
/// ・Frames Per Sprite: 1枚を何フレーム見せるか(企画書は6フレーム)
/// ・Draw Scale: 画像の大きさの倍率
/// ・Hit Scale: 当たり判定の大きさの倍率(画像の大きさが基準。余白が大きい画像は小さくする)
/// </summary>
[Serializable]
public class SkillEffectEntry
{
    [Tooltip("技のデータが呼び出す名前。左右必殺の拳は sideSpecialFist")]
    public string key;

    public Sprite[] sprites;

    [Min(1)] public int framesPerSprite = 6;
    public bool loop;

    public float drawScale = 1f;
    public Vector2 hitScale = Vector2.one;
    public int sortingOrder = 50;

    /// <summary>出てから age フレーム目(1始まり)に表示する画像。</summary>
    public Sprite GetSpriteAtAge(int age)
    {
        if (sprites == null || sprites.Length == 0) return null;

        int step = Mathf.Max(0, age - 1) / Mathf.Max(1, framesPerSprite);
        step = loop ? step % sprites.Length : Mathf.Min(step, sprites.Length - 1);

        return sprites[step];
    }
}

/// <summary>
/// 技が出す画像オブジェクト(拳など)の画像を登録する。猫のルート(PlayerController と同じオブジェクト)に付ける。
/// 登録が無い名前の技は、画像なし(判定の四角だけ)で動く。
/// </summary>
public class SkillEffectView : MonoBehaviour
{
    [SerializeField] private SkillEffectEntry[] entries;

    public bool TryGetEntry(string key, out SkillEffectEntry entry)
    {
        entry = null;
        if (entries == null) return false;

        foreach (SkillEffectEntry e in entries)
        {
            if (e != null && e.key == key)
            {
                entry = e;
                return true;
            }
        }
        return false;
    }
}
