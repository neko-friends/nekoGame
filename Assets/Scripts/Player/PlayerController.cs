using System;
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
/// キャラごとの数値。後でスプレッドシートのデータから流し込む想定の入れ物。
/// (将来は ScriptableObject に差し替えやすいよう、数値だけをここに集めている)
/// </summary>
[Serializable]
public class CharacterParams
{
    [Header("基本")]
    public float maxHp = 100f;
    public float moveSpeed = 5f;
    public float jumpSpeed = 8f;
    public int maxJumps = 2;               // 2段ジャンプ = 2
    public int inputBufferFrames = 6;      // 先行入力の猶予(フレーム)

    [Header("アイテム投げ")]
    public float throwSpeed = 10f;
    public float throwSpawnOffset = 0.5f;

    [Header("ガード")]
    public float guardMax = 100f;
    public float guardDrainPerSecond = 10f;     // 押しているだけで減る量
    public float guardRegenPerSecond = 15f;     // ガードしていない間の回復量
    public float guardPushback = 3f;            // 被弾時の押し戻し速度
    public float guardPushbackDecay = 0.8f;     // 押し戻しの減衰(1フレームごとの倍率)
    public float guardBreakStunSeconds = 2f;    // 崩壊時のスタン時間
    public float guardAfterBreakRatio = 0.5f;   // 崩壊から復帰したときの耐久値の割合

    [Header("通常攻撃(フレーム)")]
    public int attackStartupFrames = 6;
    public int attackActiveFrames = 4;
    public int attackRecoveryFrames = 14;
    public float attackLungeSpeed = 2f;         // 踏み込み速度

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
    public HitstunState Hitstun { get; private set; }
    public DeadState Dead { get; private set; }

    public int Facing { get; private set; } = 1;       // 右が1、左が-1
    public bool IsGrounded { get; private set; }
    public int JumpsLeft { get; private set; }
    public float Hp { get; private set; }
    public float GuardDurability { get; set; }
    public bool AttackHitboxActive { get; set; }       // 攻撃の判定が有効なフレームか

    public event Action<PlayerController> Died;

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

        // 状態の生成
        Machine = new StateMachine();
        Neutral = new NeutralState(this);
        Attack = new AttackState(this);
        Guard = new GuardState(this);
        GuardBreak = new GuardBreakState(this);
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
        CharacterState s = Machine.Current;
        if (s == Guard || s == GuardBreak || s == Dead) return;

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
