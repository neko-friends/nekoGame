using System;

public enum GameMode
{
    Versus,     // 人間 VS 人間
    Solo,       // 人間 VS CPU
}

public enum PlayerControlType
{
    Human,
    CPU,
    Dummy
}

[Serializable]
public class PlayerSessionData
{
    public int playerNumber;
    public PlayerControlType controlType;
    public string characterID;
}

public static class GameSession
{
    public static GameMode CurrentMode = GameMode.Versus;

    // 将来のステージ選択用
    public static string StageID = "Stage01";

    public static PlayerSessionData Player1;
    public static PlayerSessionData Player2;

    public static void ResetSession()
    {
        CurrentMode = GameMode.Versus;
        StageID = "Stage01";

        Player1 = null;
        Player2 = null;
    }
}