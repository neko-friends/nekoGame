using UnityEngine;

/// <summary>
/// キャラ1体分の数値データ。スプレッドシート(CSV)から自動生成される。
/// characterID は GameSession の characterID("Character01" など)と一致させる。
/// </summary>
[CreateAssetMenu(fileName = "CharacterData", menuName = "拒否猫戦争/Character Data")]
public class CharacterData : ScriptableObject
{
    public string characterID = "Character01";
    public string displayName = "";
    public CharacterParams parameters = new CharacterParams();
}
