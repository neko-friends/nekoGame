using UnityEngine;
using UnityEngine.InputSystem;

public class LobbyPlayer : MonoBehaviour
{
    public int PlayerNumber { get; private set; }

    public string SelectedCharacterID { get; private set; }
        = "Character01";

    public bool IsReady { get; private set; }

    public PlayerInput PlayerInput { get; private set; }

    private LobbyManager lobbyManager;

    public void Initialize(
        int playerNumber,
        LobbyManager manager,
        PlayerInput playerInput)
    {
        PlayerNumber = playerNumber;
        lobbyManager = manager;
        PlayerInput = playerInput;

        // 初参加時のキャラクターを設定
        if (string.IsNullOrEmpty(SelectedCharacterID))
        {
            SelectedCharacterID =
                playerNumber == 1
                ? "Character01"
                : "Character02";
        }

        IsReady = false;
    }

    public void SelectNextCharacter()
    {
        if (IsReady || lobbyManager == null)
            return;

        SelectedCharacterID =
            lobbyManager.GetNextCharacter(SelectedCharacterID);

        lobbyManager.RefreshLobbyUI();
    }

    public void SelectPreviousCharacter()
    {
        if (IsReady || lobbyManager == null)
            return;

        SelectedCharacterID =
            lobbyManager.GetPreviousCharacter(SelectedCharacterID);

        lobbyManager.RefreshLobbyUI();
    }

    public void ToggleReady()
    {
        if (lobbyManager == null)
            return;

        IsReady = !IsReady;

        lobbyManager.RefreshLobbyUI();
    }

    public void SetReady(bool ready)
    {
        IsReady = ready;
    }

    public PlayerSessionData CreateSessionData(
        PlayerControlType controlType)
    {
        return new PlayerSessionData
        {
            playerNumber = PlayerNumber,
            controlType = controlType,
            characterID = SelectedCharacterID
        };
    }
}