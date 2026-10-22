using UnityEngine;

/// <summary>
/// 猫の頭上にHPバーを出す。猫のルート(PlayerController と同じオブジェクト)に付ける。
/// ・画像素材は不要(単色の四角で作る)。数字(現在HP)も出せる
/// ・位置は「体の当たりの上端 + gap」。猫が動いても、上昇しても追いかける
/// ・残りの割合で色が変わる(多い: 緑 / 半分以下: 黄 / 少ない: 赤)
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class HpBarView : MonoBehaviour
{
    [Header("位置と大きさ(ワールド座標)")]
    [SerializeField] private float gap = 0.15f;                       // 頭の上端からの距離
    [SerializeField] private Vector2 barSize = new Vector2(1.2f, 0.15f);
    [SerializeField] private float borderThickness = 0.03f;

    [Header("色")]
    [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    [SerializeField] private Color highColor = new Color(0.25f, 0.85f, 0.3f);
    [SerializeField] private Color midColor = new Color(0.95f, 0.8f, 0.2f);
    [SerializeField] private Color lowColor = new Color(0.9f, 0.2f, 0.2f);
    [SerializeField, Range(0f, 1f)] private float midThreshold = 0.5f;
    [SerializeField, Range(0f, 1f)] private float lowThreshold = 0.25f;

    [Header("数字(現在のHP)")]
    [SerializeField] private bool showNumber = true;
    [SerializeField] private float numberCharacterSize = 0.06f;
    [SerializeField] private int numberFontSize = 64;
    [SerializeField] private Color numberColor = Color.white;

    [Header("描画順")]
    [SerializeField] private int sortingOrder = 90;

    private static Sprite whiteSprite;

    private PlayerController owner;
    private Transform root;
    private Transform fill;
    private SpriteRenderer fillRenderer;
    private TextMesh numberMesh;

    private void Awake()
    {
        owner = GetComponent<PlayerController>();
        Build();
    }

    private void Build()
    {
        root = new GameObject("HpBar").transform;
        root.SetParent(transform, false);

        // 枠(背景)
        CreateRect("Background", barSize + Vector2.one * borderThickness * 2f, backgroundColor, sortingOrder);

        // 中身(減っていく部分)
        Transform fillTransform = CreateRect("Fill", barSize, highColor, sortingOrder + 1);
        fill = fillTransform;
        fillRenderer = fillTransform.GetComponent<SpriteRenderer>();

        if (showNumber)
        {
            var numberGo = new GameObject("Number");
            numberGo.transform.SetParent(root, false);
            numberGo.transform.localPosition = new Vector3(0f, barSize.y * 0.5f + borderThickness + 0.12f, 0f);

            Font font = Font.CreateDynamicFontFromOSFont(
                new[] { "Arial", "Yu Gothic", "Meiryo", "Hiragino Sans" }, numberFontSize);

            numberMesh = numberGo.AddComponent<TextMesh>();
            numberMesh.font = font;
            numberMesh.fontSize = numberFontSize;
            numberMesh.characterSize = numberCharacterSize;
            numberMesh.anchor = TextAnchor.MiddleCenter;
            numberMesh.alignment = TextAlignment.Center;
            numberMesh.color = numberColor;

            var renderer = numberGo.GetComponent<MeshRenderer>();
            renderer.material = font.material;
            renderer.sortingOrder = sortingOrder + 2;
        }
    }

    // 単色の四角を1つ作る
    private Transform CreateRect(string objectName, Vector2 size, Color color, int order)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(root, false);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWhiteSprite();
        sr.color = color;
        sr.sortingOrder = order;

        return go.transform;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;

            // 1ピクセル = 1ワールド単位。拡大して使う
            whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }
        return whiteSprite;
    }

    // 猫の動きのあとに位置と中身を更新する
    private void LateUpdate()
    {
        if (owner == null || root == null) return;

        Bounds body = owner.BodyBounds;
        float halfHeight = barSize.y * 0.5f + borderThickness;
        root.position = new Vector3(body.center.x, body.max.y + gap + halfHeight, transform.position.z);

        float maxHp = Mathf.Max(1f, owner.Params.maxHp);
        float ratio = Mathf.Clamp01(owner.Hp / maxHp);

        // 左端をそろえたまま、右から減らす
        fill.localScale = new Vector3(barSize.x * ratio, barSize.y, 1f);
        fill.localPosition = new Vector3(-barSize.x * 0.5f + barSize.x * ratio * 0.5f, 0f, 0f);

        fillRenderer.color = ratio <= lowThreshold ? lowColor
                           : ratio <= midThreshold ? midColor
                           : highColor;

        if (numberMesh != null)
        {
            numberMesh.text = Mathf.CeilToInt(owner.Hp).ToString();
        }
    }
}
