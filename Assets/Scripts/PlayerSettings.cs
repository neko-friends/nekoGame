using UnityEngine;

public enum TeamSide
{
    Left,
    Right
}

public class PlayerSettings : MonoBehaviour
{
    [Header("プレイヤーの陣地")]
    [SerializeField] private TeamSide teamSide = TeamSide.Left;

    public TeamSide Side => teamSide;

    public int ForwardDirection =>
        teamSide == TeamSide.Left ? 1 : -1;

    public bool IsDefeated { get; private set; }

    public void MarkDefeated()
    {
        IsDefeated = true;
    }

    public void ResetDefeated()
    {
        IsDefeated = false;
    }
}
