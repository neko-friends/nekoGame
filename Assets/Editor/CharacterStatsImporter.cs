// このファイルは必ず Assets/Editor フォルダに置く(ビルドに含めないため)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// スプレッドシートから書き出したCSVを読み込み、キャラごとの CharacterData を作る・更新する。
///
/// CSVの形式:
///  ・見出し行 = 先頭のセルが id または characterID の行(その上の行は読み飛ばす)。
///    列名は CharacterParams の項目名(maxHp, guardMax など)と同じにする。
///    別名: id → characterID、dex → moveSpeed(unit.xlsx の列名に対応)。nameid 列は無視。
///  ・1キャラ1行。characterID(id)列が必須、displayName 列は任意。
///  ・先頭のセルが # で始まる行は読み飛ばす(説明用の行を入れてよい)。
///  ・空のセルは「変更しない」。CSVに無い列は既存の値(または初期値)のまま。
/// </summary>
public static class CharacterStatsImporter
{
    private const string OutputFolder = "Assets/Data/Characters";
    private const string IdColumn = "characterID";
    private const string NameColumn = "displayName";

    // スプレッドシート側の列名 → CharacterParams / 本ツールでの名前
    // ※ dex を移動速度(moveSpeed)として扱うのは仮の対応づけ(企画書:「移動速度はunitを参照」)
    private static readonly Dictionary<string, string> ColumnAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "id", IdColumn },
        { "dex", "moveSpeed" },
    };

    // 取り込まない列(警告も出さない)
    private static readonly HashSet<string> IgnoredColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "nameid",
    };

    private static string NormalizeColumn(string name)
    {
        return ColumnAliases.TryGetValue(name, out string alias) ? alias : name;
    }

    private static int FindHeaderRow(List<List<string>> rows)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Count == 0) continue;

            string first = rows[i][0].Trim();
            if (first.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                first.Equals(IdColumn, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return 0;
    }

    // =================================================================
    // 取り込み
    // =================================================================

    [MenuItem("Tools/拒否猫戦争/キャラステータスCSVを取り込む")]
    public static void Import()
    {
        string path = EditorUtility.OpenFilePanel("キャラステータスのCSVを選択", "", "csv");
        if (string.IsNullOrEmpty(path)) return;

        List<List<string>> rows = ParseCsv(File.ReadAllText(path, Encoding.UTF8));
        if (rows.Count == 0)
        {
            Debug.LogError("CSVが空です。");
            return;
        }

        // 見出し行 = 先頭のセルが id / characterID の行(それより上の日本語の見出し行は読み飛ばす)
        int headerRow = FindHeaderRow(rows);
        List<string> header = rows[headerRow].ConvertAll(h => NormalizeColumn(h.Trim()));
        int idIndex = header.IndexOf(IdColumn);
        int nameIndex = header.IndexOf(NameColumn);

        if (idIndex < 0)
        {
            Debug.LogError($"「{IdColumn}」(または id)列が見つかりません。");
            return;
        }

        FieldInfo[] fields = typeof(CharacterParams).GetFields(BindingFlags.Public | BindingFlags.Instance);
        var fieldMap = new Dictionary<string, FieldInfo>();
        foreach (var f in fields) fieldMap[f.Name] = f;

        // 見出しの点検(間違いに気づけるよう、ログに出す)
        var unknown = new List<string>();
        var covered = new HashSet<string>();
        foreach (string h in header)
        {
            if (h == IdColumn || h == NameColumn || h.Length == 0 || IgnoredColumns.Contains(h)) continue;
            if (fieldMap.ContainsKey(h)) covered.Add(h);
            else unknown.Add(h);
        }
        if (unknown.Count > 0)
        {
            Debug.LogWarning("CharacterParams に無い列(無視しました): " + string.Join(", ", unknown));
        }

        var missing = new List<string>();
        foreach (var f in fields) if (!covered.Contains(f.Name)) missing.Add(f.Name);
        if (missing.Count > 0)
        {
            Debug.Log("CSVに無い項目(既存の値のまま): " + string.Join(", ", missing));
        }

        Directory.CreateDirectory(OutputFolder);

        int created = 0, updated = 0;

        for (int r = headerRow + 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if (IsSkippableRow(row)) continue;

            string id = Cell(row, idIndex).Trim();
            if (id.Length == 0)
            {
                Debug.LogWarning($"{r + 1}行目: characterID が空のため読み飛ばしました。");
                continue;
            }

            string assetPath = $"{OutputFolder}/{id}.asset";
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(assetPath);
            bool isNew = data == null;

            if (isNew)
            {
                data = ScriptableObject.CreateInstance<CharacterData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }

            data.characterID = id;
            if (nameIndex >= 0 && Cell(row, nameIndex).Trim().Length > 0)
            {
                data.displayName = Cell(row, nameIndex).Trim();
            }

            for (int c = 0; c < header.Count; c++)
            {
                string column = header[c];
                if (!fieldMap.TryGetValue(column, out FieldInfo field)) continue;

                string text = Cell(row, c).Trim();
                if (text.Length == 0) continue;   // 空のセルは変更しない

                if (!TryConvert(text, field.FieldType, out object value))
                {
                    Debug.LogWarning($"{r + 1}行目 {id} の「{column}」を {field.FieldType.Name} として読めません: {text}");
                    continue;
                }

                field.SetValue(data.parameters, value);
            }

            EditorUtility.SetDirty(data);
            if (isNew) created++; else updated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"キャラステータスを取り込みました(新規 {created} 件 / 更新 {updated} 件)。保存先: {OutputFolder}");
    }

    // =================================================================
    // ひな形の書き出し(いまの CharacterParams の項目と初期値で1行作る)
    // =================================================================

    [MenuItem("Tools/拒否猫戦争/キャラステータスCSVのひな形を書き出す")]
    public static void ExportTemplate()
    {
        string path = EditorUtility.SaveFilePanel("ひな形の保存先", "", "CharacterStats_Template", "csv");
        if (string.IsNullOrEmpty(path)) return;

        FieldInfo[] fields = typeof(CharacterParams).GetFields(BindingFlags.Public | BindingFlags.Instance);
        var defaults = new CharacterParams();

        var header = new List<string> { IdColumn, NameColumn };
        var row = new List<string> { "Character01", "拒否猫" };

        foreach (var f in fields)
        {
            header.Add(f.Name);
            row.Add(Convert.ToString(f.GetValue(defaults), CultureInfo.InvariantCulture));
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", header));
        sb.AppendLine(string.Join(",", row));

        // BOM付きUTF-8(Excelで日本語が文字化けしないように)
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        Debug.Log("ひな形を書き出しました: " + path);
    }

    // =================================================================
    // 内部処理
    // =================================================================

    private static string Cell(List<string> row, int index)
    {
        return index >= 0 && index < row.Count ? row[index] : "";
    }

    private static bool IsSkippableRow(List<string> row)
    {
        if (row.Count == 0) return true;

        // 説明用の行(先頭が #)
        if (row[0].TrimStart().StartsWith("#")) return true;

        // 全部のセルが空
        foreach (string cell in row) if (cell.Trim().Length > 0) return false;
        return true;
    }

    private static bool TryConvert(string text, Type type, out object value)
    {
        value = null;

        if (type == typeof(float))
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            {
                value = f;
                return true;
            }
        }
        else if (type == typeof(int))
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
            {
                value = i;
                return true;
            }
        }
        else if (type == typeof(bool))
        {
            string t = text.ToLowerInvariant();
            if (t == "true" || t == "1") { value = true; return true; }
            if (t == "false" || t == "0") { value = false; return true; }
        }
        else if (type == typeof(string))
        {
            value = text;
            return true;
        }

        return false;
    }

    /// <summary>引用符・カンマ・改行を含むセルに対応した簡易CSVパーサ。</summary>
    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else inQuotes = false;
                }
                else cell.Append(ch);
                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    row.Add(cell.ToString());
                    cell.Length = 0;
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(cell.ToString());
                    cell.Length = 0;
                    rows.Add(row);
                    row = new List<string>();
                    break;
                default:
                    cell.Append(ch);
                    break;
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
