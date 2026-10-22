using UnityEngine;

/// <summary>
/// 猫の絵を、状態とゲームのフレームに合わせて切り替える。猫のルート(PlayerController と同じオブジェクト)に付ける。
/// ・攻撃・必殺技・ガード・スタン・のけぞり・撃破は、状態に入ってからのフレーム数(判定と同じ数え方)で切り替える
///   → 「画像4から当たり判定」のような企画書の指定と、見た目が必ずそろう
/// ・待機・走り・ジャンプ・落下は、その動きになってからのフレーム数で切り替える
/// ・向きに合わせて絵を左右反転する(絵は右向きに描いてある前提。違うときは Sprite Faces Right を切る)
/// 画像を入れていない動きは、絵を変えない。
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class CharacterAnimator : MonoBehaviour
{
    [SerializeField] private CharacterAnimationSet animationSet;

    [Tooltip("絵を表示している SpriteRenderer。未設定なら自動で探す")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("元の絵が右向きに描かれているか(右向きなら ON)")]
    [SerializeField] private bool spriteFacesRight = true;

    [Tooltip("これより速く動いていたら「走り」にする")]
    [SerializeField] private float runSpeedThreshold = 0.1f;

    [Tooltip("これより上向きに動いていたら「ジャンプ(上昇)」にする")]
    [SerializeField] private float riseSpeedThreshold = 0.1f;

    private PlayerController owner;

    // 待機・走りなど「状態のフレーム」が使えない動きの数え直し用
    private SpriteClip lastClip;
    private CharacterState lastState;
    private int clipStartFixedFrame;

    private void Awake()
    {
        owner = GetComponent<PlayerController>();

        if (spriteRenderer == null) spriteRenderer = FindSpriteRenderer();
    }

    // HP表示や技の文字の SpriteRenderer を間違えて拾わないようにする
    private SpriteRenderer FindSpriteRenderer()
    {
        var self = GetComponent<SpriteRenderer>();
        if (self != null) return self;

        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            bool isHud = false;
            for (Transform t = sr.transform; t != null && t != transform; t = t.parent)
            {
                if (t.name == "HpBar" || t.name.StartsWith("SkillCue_")) isHud = true;
            }

            if (!isHud) return sr;
        }

        return null;
    }

    // 物理(FixedUpdate)のあとの描画前に、絵を決める
    private void LateUpdate()
    {
        if (animationSet == null || spriteRenderer == null) return;

        CharacterState state = owner.Machine != null ? owner.Machine.Current : null;
        if (state == null) return;

        PickClip(state, out SpriteClip clip, out bool useStateFrames);

        if (clip != null && !clip.IsEmpty)
        {
            int frame = useStateFrames
                ? Mathf.Max(1, state.Frames)
                : FramesSinceClipChanged(clip, state);

            spriteRenderer.sprite = clip.GetSpriteAtFrame(frame);
        }

        // 向きに合わせて左右反転
        spriteRenderer.flipX = spriteFacesRight ? owner.Facing < 0 : owner.Facing > 0;
    }

    private void PickClip(CharacterState state, out SpriteClip clip, out bool useStateFrames)
    {
        useStateFrames = true;

        if (state == owner.Attack) { clip = animationSet.normalAttack; return; }
        if (state == owner.Guard) { clip = animationSet.guard; return; }
        if (state == owner.GuardBreak) { clip = animationSet.guardBreak; return; }
        if (state == owner.Hitstun) { clip = animationSet.hitstun; return; }
        if (state == owner.Dead) { clip = animationSet.dead; return; }

        if (state == owner.Special)
        {
            switch (owner.Special.CurrentSlot)
            {
                case SkillSlot.Side: clip = animationSet.sideSpecial; break;
                case SkillSlot.Up: clip = animationSet.upSpecial; break;
                case SkillSlot.Down: clip = animationSet.downSpecial; break;
                default: clip = animationSet.neutralSpecial; break;
            }
            return;
        }

        // 通常状態: 地面か空中か、動いているかで選ぶ
        useStateFrames = false;
        Vector2 velocity = owner.Body.linearVelocity;

        if (!owner.IsGrounded)
        {
            clip = velocity.y > riseSpeedThreshold ? animationSet.jumpUp : animationSet.fall;
        }
        else
        {
            clip = Mathf.Abs(velocity.x) > runSpeedThreshold ? animationSet.run : animationSet.idle;
        }
    }

    // 動きが切り替わってから、ゲームのフレームで何フレーム目か(1始まり)
    private int FramesSinceClipChanged(SpriteClip clip, CharacterState state)
    {
        int now = Mathf.RoundToInt(Time.fixedTime / Time.fixedDeltaTime);

        if (clip != lastClip || state != lastState)
        {
            lastClip = clip;
            lastState = state;
            clipStartFixedFrame = now;
        }

        return now - clipStartFixedFrame + 1;
    }
}
