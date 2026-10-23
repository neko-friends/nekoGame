using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>識別画面で選んだコントローラーを、このプレイヤーのPlayerInputへ割り当てる。</summary>
[RequireComponent(typeof(PlayerInput))]
public class PlayerSeatBinder : MonoBehaviour
{
    [SerializeField, Range(1, 2)] private int seatNumber = 1;
    [SerializeField] private PlayerInput playerInput;

    private void Awake()
    {
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
    }

    private void Start()
    {
        if (GameSession.Seats == null || seatNumber < 1 || seatNumber > GameSession.Seats.Length)
        {
            Debug.LogWarning($"PlayerSeatBinder：P{seatNumber}のGameSessionデータがありません。入力設定は変更しません。", this);
            return;
        }
        PlayerSessionData seat = GameSession.Seats[seatNumber - 1];
        if (seat == null || seat.device == null) return;
        if (playerInput == null || playerInput.actions == null)
        {
            Debug.LogWarning("PlayerSeatBinder：PlayerInputまたはInput Actionsが設定されていません。", this);
            return;
        }
        string schemeName = seat.schemeName;
        if (string.IsNullOrWhiteSpace(schemeName))
        {
            Debug.LogWarning($"PlayerSeatBinder：P{seatNumber}のControl Scheme名が空です。", this);
            return;
        }

        bool schemeExists = false;
        foreach (var scheme in playerInput.actions.controlSchemes)
        {
            if (scheme.name == schemeName) { schemeExists = true; break; }
        }
        if (!schemeExists)
        {
            Debug.LogWarning($"PlayerSeatBinder：Control Scheme「{schemeName}」がInput Actionsにありません。", this);
            return;
        }
        playerInput.SwitchCurrentControlScheme(schemeName, seat.device);
    }
}
