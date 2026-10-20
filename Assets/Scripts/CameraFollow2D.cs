
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [Header("追従対象")]
    [SerializeField] private PlayerSettings targetPlayer;
    [SerializeField] private PlayerSettings targetOpponent;

    [Header("ステージ範囲（ワールド座標）")]
    [SerializeField] private float stageLeftX = 0f;
    [SerializeField] private float stageRightX = 57.6f;

    [Header("プレイヤーの画面内位置")]
    [SerializeField, Range(0.1f, 0.9f)]
    private float leftTeamScreenX = 0.38f;

    [SerializeField, Range(0.1f, 0.9f)]
    private float rightTeamScreenX = 0.62f;

    [Header("カメラ設定")]
    [SerializeField, Min(0f)]
    private float smoothTime = 0.15f;

    [SerializeField, Min(0.01f)]
    private float anchorMoveSpeed = 8f;

    [SerializeField, Min(0.001f)]
    private float lockThreshold = 0.03f;

    [SerializeField, Min(0f)]
    private float screenMargin = 0.3f;

    private Camera cam;

    private float fixedY;
    private float fixedZ;
    private float velocityX;

    // カメラの状態
    private bool isLocked;
    private bool movingToAnchor;

    private float lockedX;
    private float targetAnchorX;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        fixedY = transform.position.y;
        fixedZ = transform.position.z;
    }

    private void LateUpdate()
    {
        if (targetPlayer == null)
            return;

        // 固定中：どちらかが倒されるまでカメラを動かさない
        if (isLocked)
        {
            if (IsEitherPlayerDefeated())
            {
                isLocked = false;
                movingToAnchor = false;
                velocityX = 0f;
            }
            else
            {
                SetCameraX(lockedX);
                return;
            }
        }

        // 固定位置へ移動中
        if (movingToAnchor)
        {
            if (IsEitherPlayerDefeated() ||
                !BothPlayersVisibleAt(targetAnchorX))
            {
                // 条件が崩れたら通常追従に戻る
                movingToAnchor = false;
                velocityX = 0f;
            }
            else
            {
                float nextX = Mathf.MoveTowards(
                    transform.position.x,
                    targetAnchorX,
                    anchorMoveSpeed * Time.deltaTime
                );

                SetCameraX(nextX);

                // 固定位置に到達した
                if (Mathf.Abs(nextX - targetAnchorX)
                    <= lockThreshold)
                {
                    SetCameraX(targetAnchorX);

                    // 到達時にも2人が画面内か確認
                    if (BothPlayersVisibleAt(targetAnchorX))
                    {
                        lockedX = targetAnchorX;
                        isLocked = true;
                    }

                    movingToAnchor = false;
                }

                return;
            }
        }

        // 通常追従
        FollowTarget();

        // 2人が画面内なら固定位置を探す
        if (targetOpponent != null &&
            !IsEitherPlayerDefeated() &&
            BothPlayersVisibleAt(transform.position.x))
        {
            if (TryFindAnchor(out float anchorX))
            {
                targetAnchorX = anchorX;
                movingToAnchor = true;
                velocityX = 0f;
            }
        }
    }

    private void FollowTarget()
    {
        float viewWidth =
            cam.orthographicSize * 2f * cam.aspect;

        float screenX =
            targetPlayer.Side == TeamSide.Left
            ? leftTeamScreenX
            : rightTeamScreenX;

        // プレイヤーを画面の左右38%・62%に配置
        float offsetX = (0.5f - screenX) * viewWidth;

        float desiredX =
            targetPlayer.transform.position.x + offsetX;

        desiredX = ClampCameraX(desiredX);

        float currentX = Mathf.SmoothDamp(
            transform.position.x,
            desiredX,
            ref velocityX,
            smoothTime
        );

        SetCameraX(ClampCameraX(currentX));
    }

    private bool TryFindAnchor(out float anchorX)
    {
        float viewWidth =
            cam.orthographicSize * 2f * cam.aspect;

        float minX = stageLeftX + viewWidth / 2f;
        float maxX = stageRightX - viewWidth / 2f;

        float centerX = (stageLeftX + stageRightX) / 2f;

        // ステージが画面より狭い場合
        if (minX > maxX)
        {
            anchorX = centerX;
            return false;
        }

        float[] anchors =
        {
            minX,       // 左端
            centerX,    // 中央
            maxX         // 右端
        };

        float currentX = transform.position.x;
        float nearestDistance = float.MaxValue;

        anchorX = currentX;
        bool found = false;

        // 2人を映せる固定位置のうち、
        // 現在のカメラに最も近い位置を選ぶ
        foreach (float anchor in anchors)
        {
            if (!BothPlayersVisibleAt(anchor))
                continue;

            float distance = Mathf.Abs(anchor - currentX);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                anchorX = anchor;
                found = true;
            }
        }

        return found;
    }

    private bool BothPlayersVisibleAt(float cameraX)
    {
        if (targetPlayer == null || targetOpponent == null)
            return false;

        float halfWidth =
            cam.orthographicSize * cam.aspect;

        float leftEdge =
            cameraX - halfWidth + screenMargin;

        float rightEdge =
            cameraX + halfWidth - screenMargin;

        float playerX = targetPlayer.transform.position.x;
        float opponentX = targetOpponent.transform.position.x;

        return playerX >= leftEdge &&
               playerX <= rightEdge &&
               opponentX >= leftEdge &&
               opponentX <= rightEdge;
    }

    private bool IsEitherPlayerDefeated()
    {
        return targetPlayer.IsDefeated ||
               (targetOpponent != null &&
                targetOpponent.IsDefeated);
    }

    private float ClampCameraX(float x)
    {
        float viewWidth =
            cam.orthographicSize * 2f * cam.aspect;

        float minX = stageLeftX + viewWidth / 2f;
        float maxX = stageRightX - viewWidth / 2f;

        if (minX > maxX)
            return (stageLeftX + stageRightX) / 2f;

        return Mathf.Clamp(x, minX, maxX);
    }

    private void SetCameraX(float x)
    {
        transform.position = new Vector3(
            x,
            fixedY,
            fixedZ
        );
    }

    // 追従対象を切り替えるための公開メソッド
    public void SetTarget(
        PlayerSettings newTarget,
        PlayerSettings newOpponent)
    {
        targetPlayer = newTarget;
        targetOpponent = newOpponent;

        isLocked = false;
        movingToAnchor = false;
        velocityX = 0f;
    }
}
