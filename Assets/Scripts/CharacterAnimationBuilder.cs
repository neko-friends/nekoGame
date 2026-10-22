// このファイルは必ず Assets/Editor フォルダに置く(ビルドに含めないため)
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// フォルダの中の画像(PNG)を、ファイル名のルールで動きごとに振り分けて
/// CharacterAnimationSet を作る(すでにあれば、見つかった動きだけ更新する)ツール。
///
/// ファイル名のルール: 「動きの名前」+「番号」。区切りは _ - 空白 のどれか、または無し。
///   例) normalAttack_1.png 〜 normalAttack_5.png / idle_1.png / 通常攻撃1.png
/// 番号は数字の大きさで並べる(2 の次が 10 でも正しい順になる)。
///
/// 動きの名前(どれを使ってもよい):
///   idle / 待機                         run / 走り
///   jumpUp / jump / ジャンプ            fall / 落下
///   guard / ガード                      guardBreak / stun / スタン
///   hitstun / hit / のけぞり            dead / 撃破
///   normalAttack / attack / 通常攻撃    neutralSpecial / specialN / N必殺
///   sideSpecial / specialSide / 左右必殺
///   upSpecial / specialUp / 上必殺      downSpecial / specialDown / 下必殺
/// </summary>
public class CharacterAnimationBuilder : EditorWindow
{
    private enum PivotMode { Center, BottomCenter }

    private Object imageFolder;
    private string assetName = "KyohiNeko_Anim";
    private string characterID = "unit0001";
    private string outputFolder = "Assets/Data/Animations";

    private bool fixImportSettings = true;
    private float pixelsPerUnit = 100f;
    private PivotMode pivot = PivotMode.Center;
    private int framesPerSprite = 6;

    private string report = "";
    private Vector2 scroll;

    // 名前(小文字・区切りなし)→ 動きの種類
    private static readonly Dictionary<string, string> Aliases = BuildAliases();

    private static Dictionary<string, string> BuildAliases()
    {
        var d = new Dictionary<string, string>();

        void Add(string key, params string[] names)
        {
            d[Normalize(key)] = key;
            foreach (string n in names) d[Normalize(n)] = key;
        }

        Add("idle", "待機");
        Add("run", "走り", "走る");
        Add("jumpUp", "jump", "ジャンプ");
        Add("fall", "落下");
        Add("guard", "ガード");
        Add("guardBreak", "stun", "スタン", "ガード崩壊");
        Add("hitstun", "hit", "hurt", "のけぞり", "被弾");
        Add("dead", "撃破");
        Add("normalAttack", "attack", "通常攻撃", "攻撃");
        Add("neutralSpecial", "specialN", "specialNeutral", "N必殺");
        Add("sideSpecial", "specialSide", "左右必殺");
        Add("upSpecial", "specialUp", "上必殺");
        Add("downSpecial", "specialDown", "下必殺");

        return d;
    }

    private static string Normalize(string s)
    {
        return s.Replace("_", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();
    }

    [MenuItem("Tools/拒否猫戦争/アニメーションセット作成ツール")]
    private static void Open()
    {
        var window = GetWindow<CharacterAnimationBuilder>("アニメーションセット作成");
        window.minSize = new Vector2(420f, 420f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("画像フォルダから作成", EditorStyles.boldLabel);

        imageFolder = EditorGUILayout.ObjectField(
            new GUIContent("画像のフォルダ", "Projectウィンドウのフォルダをここへドラッグ"),
            imageFolder, typeof(DefaultAsset), false);

        assetName = EditorGUILayout.TextField("作るアセットの名前", assetName);
        characterID = EditorGUILayout.TextField("キャラID", characterID);
        outputFolder = EditorGUILayout.TextField("保存先フォルダ", outputFolder);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("画像の取り込み設定", EditorStyles.boldLabel);

        fixImportSettings = EditorGUILayout.Toggle(
            new GUIContent("設定をそろえる", "見つかった画像を Sprite にして、下の値にそろえる"), fixImportSettings);

        using (new EditorGUI.DisabledScope(!fixImportSettings))
        {
            pixelsPerUnit = EditorGUILayout.FloatField("Pixels Per Unit", pixelsPerUnit);
            pivot = (PivotMode)EditorGUILayout.EnumPopup("Pivot", pivot);
        }

        EditorGUILayout.Space();
        framesPerSprite = EditorGUILayout.IntField(
            new GUIContent("1枚のフレーム数(新規作成時)", "企画書の技は6フレーム"), framesPerSprite);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(imageFolder == null))
        {
            if (GUILayout.Button("作成 / 更新", GUILayout.Height(32f)))
            {
                Build();
            }
        }

        if (!string.IsNullOrEmpty(report))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("結果", EditorStyles.boldLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(report, MessageType.None);
            EditorGUILayout.EndScrollView();
        }
    }

    // =================================================================
    // 作成
    // =================================================================

    private void Build()
    {
        var sb = new StringBuilder();

        string folderPath = AssetDatabase.GetAssetPath(imageFolder);
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            report = "画像のフォルダには、Assets の中のフォルダを指定してください。";
            return;
        }

        framesPerSprite = Mathf.Max(1, framesPerSprite);

        // 1. 画像を集めて、動きごとに番号順に並べる
        var groups = new Dictionary<string, SortedDictionary<int, string>>();   // 動き → (番号 → パス)
        var skipped = new List<string>();

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path);

            Match m = Regex.Match(fileName, @"^(?<name>.*?)[ _\-]*(?<num>\d+)$");
            if (!m.Success)
            {
                skipped.Add($"{fileName}(番号がありません)");
                continue;
            }

            if (!Aliases.TryGetValue(Normalize(m.Groups["name"].Value), out string key))
            {
                skipped.Add($"{fileName}(動きの名前が分かりません)");
                continue;
            }

            int number = int.Parse(m.Groups["num"].Value);

            if (!groups.TryGetValue(key, out var byNumber))
            {
                byNumber = new SortedDictionary<int, string>();
                groups[key] = byNumber;
            }

            if (byNumber.ContainsKey(number))
            {
                skipped.Add($"{fileName}(同じ番号がすでにあります)");
                continue;
            }

            byNumber[number] = path;
        }

        if (groups.Count == 0)
        {
            report = "振り分けできる画像がありませんでした。\n\n" +
                     "ファイル名を「動きの名前+番号」にしてください。例: normalAttack_1.png\n" +
                     (skipped.Count > 0 ? "\n読み飛ばし:\n  " + string.Join("\n  ", skipped) : "");
            return;
        }

        // 2. 取り込み設定をそろえる
        if (fixImportSettings)
        {
            int changed = FixImportSettings(groups);
            sb.AppendLine($"画像の取り込み設定を {changed} 枚そろえました。");
        }

        // 3. アセットを用意する(あれば更新、無ければ新規)
        EnsureFolder(outputFolder);
        string assetPath = $"{outputFolder}/{assetName}.asset";

        var set = AssetDatabase.LoadAssetAtPath<CharacterAnimationSet>(assetPath);
        bool isNew = set == null;

        if (isNew)
        {
            set = CreateInstance<CharacterAnimationSet>();
            AssetDatabase.CreateAsset(set, assetPath);
        }

        set.characterID = characterID;

        // 4. 動きごとに画像を入れる
        var found = new List<string>();

        foreach (var pair in groups)
        {
            SpriteClip clip = GetClip(set, pair.Key);
            if (clip == null) continue;

            var sprites = new List<Sprite>();
            foreach (var entry in pair.Value)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.Value);
                if (sprite == null)
                {
                    skipped.Add($"{Path.GetFileName(entry.Value)}(Sprite として読めません)");
                    continue;
                }
                sprites.Add(sprite);
            }

            if (sprites.Count == 0) continue;

            clip.sprites = sprites.ToArray();
            if (isNew) clip.framesPerSprite = framesPerSprite;

            // 左右必殺: 画像1〜3 なら、企画書どおり 1,2,3,2,3,2,3,2,3,2,3 にする(すでに設定があれば触らない)
            if (pair.Key == "sideSpecial" && sprites.Count == 3 && (clip.sequence == null || clip.sequence.Length == 0))
            {
                clip.sequence = new[] { 1, 2, 3, 2, 3, 2, 3, 2, 3, 2, 3 };
                sb.AppendLine("左右必殺: 企画書どおり「画像2と3を5回繰り返す」並びにしました。");
            }

            found.Add($"{pair.Key}: {sprites.Count}枚");
        }

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = set;
        EditorGUIUtility.PingObject(set);

        // 5. 結果の表示
        sb.Insert(0, (isNew ? "新規作成しました: " : "更新しました: ") + assetPath + "\n\n");
        sb.AppendLine();
        sb.AppendLine("画像を入れた動き:");
        foreach (string f in found) sb.AppendLine("  " + f);

        var missing = new List<string>();
        foreach (string key in AllClipKeys)
        {
            if (!groups.ContainsKey(key)) missing.Add(key);
        }
        if (missing.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("画像が無い動き(そのままの絵になります):");
            sb.AppendLine("  " + string.Join(", ", missing));
        }

        if (skipped.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("読み飛ばした画像:");
            foreach (string s in skipped) sb.AppendLine("  " + s);
        }

        report = sb.ToString();
    }

    // =================================================================
    // 内部処理
    // =================================================================

    private static readonly string[] AllClipKeys =
    {
        "idle", "run", "jumpUp", "fall", "guard", "guardBreak", "hitstun", "dead",
        "normalAttack", "neutralSpecial", "sideSpecial", "upSpecial", "downSpecial"
    };

    private static SpriteClip GetClip(CharacterAnimationSet set, string key)
    {
        switch (key)
        {
            case "idle": return set.idle;
            case "run": return set.run;
            case "jumpUp": return set.jumpUp;
            case "fall": return set.fall;
            case "guard": return set.guard;
            case "guardBreak": return set.guardBreak;
            case "hitstun": return set.hitstun;
            case "dead": return set.dead;
            case "normalAttack": return set.normalAttack;
            case "neutralSpecial": return set.neutralSpecial;
            case "sideSpecial": return set.sideSpecial;
            case "upSpecial": return set.upSpecial;
            case "downSpecial": return set.downSpecial;
            default: return null;
        }
    }

    // 見つかった画像を Sprite(1枚絵)にして、Pixels Per Unit とピボットをそろえる
    private int FixImportSettings(Dictionary<string, SortedDictionary<int, string>> groups)
    {
        int changed = 0;
        SpriteAlignment alignment = pivot == PivotMode.BottomCenter
            ? SpriteAlignment.BottomCenter
            : SpriteAlignment.Center;

        foreach (var group in groups.Values)
        {
            foreach (string path in group.Values)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);

                bool needsChange =
                    importer.textureType != TextureImporterType.Sprite ||
                    importer.spriteImportMode != SpriteImportMode.Single ||
                    !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit) ||
                    settings.spriteAlignment != (int)alignment;

                if (!needsChange) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;

                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)alignment;
                importer.SetTextureSettings(settings);

                importer.SaveAndReimport();
                changed++;
            }
        }

        return changed;
    }

    // "Assets/Data/Animations" のような階層を、無ければ作る
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string name = Path.GetFileName(path);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }
}
