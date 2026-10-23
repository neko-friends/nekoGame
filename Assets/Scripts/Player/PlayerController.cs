using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>必殺技の方向入力(キャラの向き基準。「前」は向いている側)。</summary>
public enum SpecialDirection { Neutral, Forward, Up, Down }

/// <summary>攻撃を受けたときに渡す情報。攻撃側が作り、ReceiveHit に渡す。</summary>
public struct HitInfo
{
    public float damage;          // HPへのダメージ
    public Vector2 knockback;     // ノックバック速度(ワールド座標。左右の向きは攻撃側が決める)
    public int hitstunFrames;     // のけぞりフレーム数
    public float guardDamage;     // ガード耐久値へのダメージ
}

/// <summary>
/// キャラごとの数値。スプレッドシート(CSV)から CharacterData に流し込む入れ物。
/// 項目名はCSVの列見出しと一致させる(エディタの取り込みが項目名で対応づける)。
/// ※ 既存の項目名は変更していない。企画書に合わせて既定値だけ更新し、項目を追加した。
/// </summary>
[Serializable]
public class CharacterParams
{
    [Header("基本")]
    public float maxHp = 100f;
    [Tooltip("【仮値】企画書に数値の記載なし。ダメージ = atk × 技補正値(次のステップで使用)")]
    public float atk = 100f;
    public float moveSpeed = 5f;
    public float jumpSpeed = 8f;
    public int maxJumps = 2;               // 2段ジャンプ = 2
    public int inputBufferFrames = 6;      // 先行入力の猶予(フレーム)

    [Header("アイテム投げ")]
    public float throwSpeed = 10f;
    public float throwSpawnOffset = 0.5f;

    [Header("ガード(企画書の値)")]
    public float guardMax = 1000f;
    public float guardDrainDelaySeconds = 0.5f;          // ガード開始からこの時間後に自然消費が始まる
    public float guardDrainPerSecond = 250f;             // 自然消費の速度
    public float guardDrainPauseAfterHitSeconds = 0.5f;  // 被弾で耐久値が減ったあと、自然消費を止める時間
    public float guardRegenDelaySeconds = 3f;            // ガードをやめてから回復が始まるまで
    public float guardRegenPerSecond = 25f;              // 回復の速度
    public float guardPushback = 3f;                     // 被弾時の押し戻し速度
    public float guardPushbackDecay = 0.8f;              // 押し戻しの減衰(1フレームごとの倍率)
    public float guardBreakStunSeconds = 2f;             // 崩壊時のスタン時間
    public float guardAfterBreakRatio = 1f;              // 崩壊から復帰したときの耐久値の割合(1 = 全回復)

    [Header("技補正値(企画書:拒否猫の値)")]
    public float normalAttackMultiplier = 0.5f;
    public float neutralSpecialMultiplier = 1.2f;
    public float sideSpecialMultiplier = 0.75f;
    public float upSpecialMultiplier = 1.5f;
    public float downSpecialMultiplier = 1f;

    [Header("通常攻撃(フレーム)")]
    public int attackStartupFrames = 6;
    public int attackActiveFrames = 4;
    public int attackRecoveryFrames = 14;
    public float attackLungeSpeed = 2f;         // 踏み込み速度

    [Header("通常攻撃の当たり(【仮値】企画書に記載なし。調整してよい)")]
    public float attackKnockbackX = 4f;         // ノックバック(横。向いている側へ飛ばす)
    public float attackKnockbackY = 3f;         // ノックバック(縦)
    public int attackHitstunFrames = 20;        // 相手ののけぞりフレーム数

    [Header("通常攻撃の文字・音声(企画書:拒否猫は24フレで「がんばえ」)")]
    public string attackCueText = "がんばえ";
    public int attackCueFrame = 24;             // 0 なら出さない

    [Header("のけぞり")]
    public float knockbackDecayPerFrame = 0.92f; // ノックバックの減衰(1フレームごとの倍率)
}

/// <summary>
/// キャラの「体」。入力は PlayerInputReceiver から読み取り、行動は状態機械が決める。
/// このクラスは「共通の部品(移動・ジャンプ・接地・アイテム・被弾)」を提供し、
/// 「いま何ができるか」は各 State が判断する。
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("入力の受け取り元(未設定なら同じオブジェクトから自動取得)")]
    [SerializeField] private PlayerInputReceiver inputReceiver;

    [Header("キャラの数値")]
    [Tooltip("設定すると、この数値(スプレッドシートから取り込んだもの)を使う。未設定なら下の Parameters を使う")]
    [SerializeField] private CharacterData characterData;
    [SerializeField] private CharacterParams parameters = new CharacterParams();

    [Header("デバッグ")]
    [Tooltip("状態ごとに色を変える仮の絵(任意)")]
    [SerializeField] private SpriteRenderer debugRenderer;
    [SerializeField] private bool showDebugLabel = true;

    // ---- 外部(各State・他スクリプト)から見えるもの ----
    public PlayerInputReceiver Controls => inputReceiver;
    public CharacterParams Params => parameters;
    public Rigidbody2D Body { get; private set; }
    public StateMachine Machine { get; private set; }

    public NeutralState Neutral { get; private set; }
    public AttackState Attack { get; private set; }
    public GuardState Guard { get; private set; }
    public GuardBreakState GuardBreak { get; private set; }
    public SkillState Special { get; private set; }
    public HitstunState Hitstun { get; private set; }
    public DeadState Dead { get; private set; }

    public int Facing { get; private set; } = 1;       // 右が1、左が-1
    public bool IsGrounded { get; private set; }
    public int JumpsLeft { get; private set; }
    public float Hp { get; private set; }
    public float GuardDurability { get; set; }
    /// <summary>復活後など、攻撃を受けない時間かどうか。</summary>
    public bool IsInvincible { get; set; }
    public bool AttackHitboxActive { get; set; }       // 攻撃の判定が有効なフレームか

    // ガードの待ち時間(フレーム)。GuardState が設定し、FixedUpdate で減らす
    public int GuardDrainHoldFrames { get; set; }      // 被弾後、自然消費を止める残りフレーム
    public int GuardRegenWaitFrames { get; set; }      // ガードをやめてから、回復が始まるまでの残りフレーム

    public event Action<PlayerController> Died;

    /// <summary>技の文字・音声を出す合図(SkillCueView が受け取る)。</summary>
    public event Action<string> SkillCue;

    /// <summary>キャラのID。CharacterData が無いときは拒否猫として扱う。</summary>
    public string CharacterId =>
        characterData != null ? characterData.characterID : SkillLibrary.DefaultCharacterId;

    /// <summary>体の当たり(Collider2D)の範囲。必殺技の判定の基準に使う。</summary>
    public Bounds BodyBounds => bodyCollider.bounds;

    /// <summary>必殺技の判定の範囲(シーンビューで確認する用)。SkillState が毎フレーム更新する。</summary>
    public readonly List<(Vector2 center, Vector2 size)> ActiveSkillBoxes =
        new List<(Vector2 center, Vector2 size)>();

    public void RaiseCue(string text)
    {
        SkillCue?.Invoke(text);
    }

    // ---- 内部 ----
    private Collider2D bodyCollider;
    private readonly ContactPoint2D[] contacts = new ContactPoint2D[8];

    private GameObject heldItemPrefab;   // 持っているアイテム
    private Item nearbyItem;             // 拾える範囲にあるアイテム

    private Transform posePanel;

    // =================================================================
    // Unity イベント
    // =================================================================
    private void Awake()
    {
        Body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();

        // 倒れない・眠らない設定(接地判定が接触情報に頼るため)
        Body.freezeRotation = true;
        Body.sleepMode = RigidbodySleepMode2D.NeverSleep;

        if (inputReceiver == null)
        {
            inputReceiver = GetComponent<PlayerInputReceiver>();
        }

        // CharacterData が設定されていれば、その数値を使う(初期値を使う前に適用する)
        if (characterData != null)
        {
            ApplyCharacterData(characterData);
        }

        // 状態の生成
        Machine = new StateMachine();
        Neutral = new NeutralState(this);
        Attack = new AttackState(this);
        Guard = new GuardState(this);
        GuardBreak = new GuardBreakState(this);
        Special = new SkillState(this);
        Hitstun = new HitstunState(this);
        Dead = new DeadState(this);
        Machine.Changed += OnStateChanged;

        // 初期値
        Hp = parameters.maxHp;
        GuardDurability = parameters.guardMax;
        JumpsLeft = parameters.maxJumps;

        // ポーズ画面(試合管理側へ移すまでは従来どおりここで探す)
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            posePanel = canvas.transform.Find("PosePanel");
        }

        Machine.Change(Neutral);
    }

    private void Start()
    {
        if (posePanel != null)
        {
            posePanel.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("PosePanelが見つかりません。Canvas内の名前を確認してください。");
        }

        if (inputReceiver == null)
        {
            Debug.LogError("PlayerInputReceiverが見つかりません。同じオブジェクトに追加してください。");
        }
    }

    // ポーズはゲームの進行(状態機械)とは無関係に受け付ける
    private void Update()
    {
        if (inputReceiver == null) return;

        if (inputReceiver.Pose.TryConsumePress(parameters.inputBufferFrames))
        {
            OpenPose();
        }
    }

    // ゲームの進行はすべて FixedUpdate(固定フレーム)で行う
    private void FixedUpdate()
    {
        if (inputReceiver == null) return;

        inputReceiver.AdvanceFrame();   // 入力バッファの時間を進める
        UpdateGrounded();               // 接地とジャンプ回数の管理
        RegenerateGuard();              // ガード耐久値の回復
        Machine.Tick();                 // 現在の状態を1フレーム進める
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Item")) return;

        Item item = other.GetComponent<Item>();
        if (item != null) nearbyItem = item;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (nearbyItem != null && other.gameObject == nearbyItem.gameObject)
        {
            nearbyItem = null;
        }
    }

    // =================================================================
    // キャラデータの適用
    // =================================================================

    /// <summary>
    /// CharacterData の数値をコピーして使う(元のアセットを書き換えないため複製する)。
    /// 試合シーンで GameSession の characterID から選んだデータを渡す場合にも使える。
    /// </summary>
    public void ApplyCharacterData(CharacterData data)
    {
        if (data == null) return;

        characterData = data;
        parameters = JsonUtility.FromJson<CharacterParams>(JsonUtility.ToJson(data.parameters));
    }

    // =================================================================
    // 各Stateが使う共通の部品
    // =================================================================

    /// <summary>入力が一定以上のときだけ向きを変える。</summary>
    public void Face(float x)
    {
        if (x > 0.01f) Facing = 1;
        else if (x < -0.01f) Facing = -1;
    }

    // 速度を書き込むのは「いまの状態」だけにする(取り合いを防ぐ)
    public void SetVelocityX(float x)
    {
        Body.linearVelocity = new Vector2(x, Body.linearVelocity.y);
    }

    public void SetVelocity(Vector2 velocity)
    {
        Body.linearVelocity = velocity;
    }

    /// <summary>ジャンプ。残り回数がなければ何もしない。</summary>
    public bool TryJump()
    {
        if (JumpsLeft <= 0) return false;

        JumpsLeft--;
        Body.linearVelocity = new Vector2(Body.linearVelocity.x, parameters.jumpSpeed);
        return true;
    }

    /// <summary>必殺ボタンを押したときの方向(キャラの向き基準)。上下が優先。</summary>
    public SpecialDirection ReadSpecialDirection()
    {
        Vector2 m = inputReceiver.MoveInput;

        if (m.y > 0.5f) return SpecialDirection.Up;
        if (m.y < -0.5f) return SpecialDirection.Down;
        if (m.x * Facing > 0.5f) return SpecialDirection.Forward;
        return SpecialDirection.Neutral;
    }

    /// <summary>投擲ボタン: アイテムを持っていれば投げ、持っていなければ近くのものを拾う。</summary>
    public void ThrowOrPickup()
    {
        if (heldItemPrefab != null)
        {
            ThrowHeldItem();
            return;
        }

        if (nearbyItem != null && nearbyItem.ItemPrefab != null)
        {
            heldItemPrefab = nearbyItem.ItemPrefab;
            Destroy(nearbyItem.gameObject);
            nearbyItem = null;
            Debug.Log("アイテムを拾った！");
        }
    }

    public int SecondsToFrames(float seconds)
    {
        return Mathf.Max(1, Mathf.RoundToInt(seconds / Time.fixedDeltaTime));
    }

    // =================================================================
    // 被弾・撃破・復活
    // =================================================================

    /// <summary>攻撃を受ける。ガード中ならダメージを防ぎ、そうでなければのけぞる。</summary>
    public void ReceiveHit(HitInfo hit)
    {
        if (IsInvincible) return;
        if (Machine.Current == Dead) return;

        // ガード中: ダメージなし。耐久値だけが減る
        if (Machine.Current == Guard)
        {
            Guard.Absorb(hit);
            return;
        }

        Hp -= hit.damage;

        if (Hp <= 0f)
        {
            Hp = 0f;
            Machine.Change(Dead);
            return;
        }

        // ガード崩壊中はダメージだけ受ける(スタンは延長も中断もしない)
        if (Machine.Current == GuardBreak) return;

        Hitstun.Begin(hit);
        Machine.Change(Hitstun);
    }

    /// <summary>DeadState から呼ばれる。撃破の通知(進行権の処理などは試合管理側が購読する)。</summary>
    public void NotifyDied()
    {
        Died?.Invoke(this);
    }

    /// <summary>復活。試合管理側から呼ぶ想定。</summary>
    public void Revive(Vector2 position)
    {
        Hp = parameters.maxHp;
        GuardDurability = parameters.guardMax;
        GuardDrainHoldFrames = 0;
        GuardRegenWaitFrames = 0;
        JumpsLeft = parameters.maxJumps;
        AttackHitboxActive = false;

        Body.position = position;
        Body.linearVelocity = Vector2.zero;
        inputReceiver.ClearBuffers();

        Machine.Change(Neutral);
    }

    // =================================================================
    // 内部処理
    // =================================================================

    private void UpdateGrounded()
    {
        bool wasGrounded = IsGrounded;
        IsGrounded = CheckGround();

        if (IsGrounded && Body.linearVelocity.y <= 0.01f)
        {
            // 着地: ジャンプ回数を回復(上昇中は回復させない)
            JumpsLeft = parameters.maxJumps;
        }
        else if (wasGrounded && !IsGrounded && JumpsLeft >= parameters.maxJumps)
        {
            // ジャンプせずに地面から離れた(落下): 1回分を消費した扱い
            JumpsLeft = parameters.maxJumps - 1;
        }
    }

    // 足元で Ground タグの物体に接触しているか(壁の側面・天井は除く)
    private bool CheckGround()
    {
        int count = Body.GetContacts(contacts);
        float centerY = bodyCollider.bounds.center.y;

        for (int i = 0; i < count; i++)
        {
            ContactPoint2D c = contacts[i];

            bool isGroundTag =
                c.collider.CompareTag("Ground") || c.otherCollider.CompareTag("Ground");

            // 法線がほぼ上下向き、かつ接触点がキャラの中心より下
            if (isGroundTag && Mathf.Abs(c.normal.y) > 0.7f && c.point.y < centerY)
            {
                return true;
            }
        }

        return false;
    }

    private void RegenerateGuard()
    {
        // 被弾後の「自然消費ストップ」の残り時間は、状態に関係なく進める
        if (GuardDrainHoldFrames > 0) GuardDrainHoldFrames--;

        CharacterState s = Machine.Current;
        if (s == Guard || s == GuardBreak || s == Dead) return;

        // ガードをやめてから一定時間は回復しない(企画書:3秒後から回復)
        if (GuardRegenWaitFrames > 0)
        {
            GuardRegenWaitFrames--;
            return;
        }

        GuardDurability = Mathf.Min(
            parameters.guardMax,
            GuardDurability + parameters.guardRegenPerSecond * Time.fixedDeltaTime
        );
    }

    private void ThrowHeldItem()
    {
        // 入力方向へ投げる。入力がなければ向いている方向
        Vector2 aim = inputReceiver.MoveInput;
        Vector2 direction = aim.sqrMagnitude > 0.25f
            ? aim.normalized
            : Vector2.right * Facing;

        Vector3 spawnPosition =
            transform.position + (Vector3)(direction * parameters.throwSpawnOffset);

        GameObject item = Instantiate(heldItemPrefab, spawnPosition, Quaternion.identity);

        Rigidbody2D itemRb = item.GetComponent<Rigidbody2D>();
        if (itemRb != null)
        {
            itemRb.linearVelocity = direction * parameters.throwSpeed;
        }
        else
        {
            Debug.LogWarning("投げたアイテムにRigidbody2Dがありません。");
        }

        heldItemPrefab = null;
    }

    private void OpenPose()
    {
        if (posePanel != null)
        {
            posePanel.gameObject.SetActive(true);
        }
    }

    private void OnStateChanged(CharacterState previous, CharacterState next)
    {
        if (debugRenderer != null)
        {
            debugRenderer.color = next.DebugColor;
        }
    }

    // =================================================================
    // デバッグ
    // =================================================================

    // インスペクターの ︙ メニューから実行できるテスト用の被弾
    [ContextMenu("テスト: 被弾(左から攻撃された想定)")]
    private void DebugTakeHit()
    {
        ReceiveHit(new HitInfo
        {
            damage = 10f,
            knockback = new Vector2(4f, 3f),
            hitstunFrames = 20,
            guardDamage = 25f
        });
    }

    // シーンビューで必殺技の判定を見る(技の最中だけ表示)
    private void OnDrawGizmos()
    {
        if (ActiveSkillBoxes == null) return;

        Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
        foreach (var box in ActiveSkillBoxes)
        {
            Gizmos.DrawCube(box.center, box.size);
        }
    }

    private void OnGUI()
    {
        if (!showDebugLabel || Camera.main == null || Machine == null || Machine.Current == null) return;

        Vector3 p = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.2f);

        GUI.Label(
            new Rect(p.x - 70f, Screen.height - p.y - 40f, 160f, 60f),
            $"{Machine.Current.Name}\nHP {Hp:0}  Guard {GuardDurability:0}  Jump {JumpsLeft}"
        );
    }
}