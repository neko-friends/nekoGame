using UnityEngine;

/// <summary>1フィールド用の横スクロールカメラ。縦位置は固定する。</summary>
[RequireComponent(typeof(Camera))]
public class MatchCameraController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private MatchFlowManager matchFlowManager;
    [Header("フィールド")]
    [SerializeField] private float fieldCenterX = 0f;
    [SerializeField, Min(1f)] private float fieldLengthCats = 30f;
    [Header("画面サイズ（猫の幅を1ユニットとして計算）")]
    [SerializeField, Min(1f)] private float catsOnScreen = 10f;
    [SerializeField, Min(0.01f)] private float catWidthUnits = 1f;
    [Header("追従")]
    [SerializeField, Min(0f)] private float followSmoothTime = 0.15f;
    [Header("任意：画面端への押し戻し")]
    [SerializeField] private bool confinePlayers = false;
    [SerializeField, Min(0f)] private float playerEdgeMargin = 0.5f;

    private Camera targetCamera;
    private float fixedY;
    private float fixedZ;
    private float velocityX;
    private TeamSide focusSide = TeamSide.Left;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        fixedY = transform.position.y;
        fixedZ = transform.position.z;
        if (matchFlowManager == null) matchFlowManager = FindFirstObjectByType<MatchFlowManager>();
        ConfigureCameraWidth();
    }

    private void LateUpdate()
    {
        if (targetCamera == null) return;
        ConfigureCameraWidth();
        if (matchFlowManager == null)
        {
            SetCameraX(ClampCameraX(fieldCenterX));
            return;
        }

        PlayerController left = matchFlowManager.GetPlayer(TeamSide.Left);
        PlayerController right = matchFlowManager.GetPlayer(TeamSide.Right);
        bool leftAlive = matchFlowManager.IsAlive(TeamSide.Left) && left != null;
        bool rightAlive = matchFlowManager.IsAlive(TeamSide.Right) && right != null;

        if (!matchFlowManager.HasAnyDeathOccurred)
        {
            velocityX = 0f;
            SetCameraX(ClampCameraX(fieldCenterX));
            return;
        }

        if (focusSide == TeamSide.Left && !leftAlive && rightAlive) focusSide = TeamSide.Right;
        else if (focusSide == TeamSide.Right && !rightAlive && leftAlive) focusSide = TeamSide.Left;
        else if (!leftAlive && rightAlive) focusSide = TeamSide.Right;
        else if (!rightAlive && leftAlive) focusSide = TeamSide.Left;

        if (confinePlayers && leftAlive && rightAlive)
        {
            float halfWidth = VisibleWorldWidth * 0.5f;
            float minPlayerX = transform.position.x - halfWidth + playerEdgeMargin;
            float maxPlayerX = transform.position.x + halfWidth - playerEdgeMargin;
            ConfinePlayerX(left, minPlayerX, maxPlayerX);
            ConfinePlayerX(right, minPlayerX, maxPlayerX);
            velocityX = 0f;
            return;
        }

        PlayerController focus = focusSide == TeamSide.Left ? left : right;
        if (focus == null || !matchFlowManager.IsAlive(focusSide))
        {
            velocityX = 0f;
            return;
        }
        float targetX = ClampCameraX(focus.transform.position.x);
        float nextX = followSmoothTime <= 0f
            ? targetX
            : Mathf.SmoothDamp(transform.position.x, targetX, ref velocityX, followSmoothTime);
        SetCameraX(ClampCameraX(nextX));
    }

    private float VisibleWorldWidth => targetCamera.orthographicSize * 2f * targetCamera.aspect;

    private void ConfigureCameraWidth()
    {
        if (targetCamera == null || !targetCamera.orthographic || targetCamera.aspect <= 0f) return;
        float desiredWidth = catsOnScreen * catWidthUnits;
        targetCamera.orthographicSize = desiredWidth / (2f * targetCamera.aspect);
    }

    private float ClampCameraX(float x)
    {
        float fieldHalf = fieldLengthCats * catWidthUnits * 0.5f;
        float viewHalf = VisibleWorldWidth * 0.5f;
        float minX = fieldCenterX - fieldHalf + viewHalf;
        float maxX = fieldCenterX + fieldHalf - viewHalf;
        if (minX > maxX) return fieldCenterX;
        return Mathf.Clamp(x, minX, maxX);
    }

    private void SetCameraX(float x)
    {
        transform.position = new Vector3(x, fixedY, fixedZ);
    }

    private void ConfinePlayerX(PlayerController player, float minX, float maxX)
    {
        if (player == null || player.Body == null) return;
        float x = player.transform.position.x;
        if (x >= minX && x <= maxX) return;
        x = Mathf.Clamp(x, minX, maxX);
        Vector2 position = player.Body.position;
        position.x = x;
        player.Body.position = position;
        Vector2 velocity = player.Body.linearVelocity;
        if ((x <= minX && velocity.x < 0f) || (x >= maxX && velocity.x > 0f)) velocity.x = 0f;
        player.Body.linearVelocity = velocity;
    }
}
