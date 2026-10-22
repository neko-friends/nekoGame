
using UnityEngine;

[System.Serializable]
public class TeamSpawnPoints
{
    [Header("試合開始時：中央")]
    public GameObject initialCenter;

    [Header("初デス後：中央フィールドの自陣側の端")]
    public GameObject centerFieldHomeEdge;

    [Header("各フィールドの関門前：左・中央・右")]
    public GameObject[] gateBeforeByField = new GameObject[3];

    [Header("各フィールドの城前：左・中央・右")]
    public GameObject[] castleFrontByField = new GameObject[3];
}

public class RespawnPointManager : MonoBehaviour
{
    [Header("進行管理")]
    [SerializeField]
    private TeamProgressManager progressManager;

    [Header("左陣営の復活地点")]
    [SerializeField]
    private TeamSpawnPoints leftSpawnPoints = new TeamSpawnPoints();

    [Header("右陣営の復活地点")]
    [SerializeField]
    private TeamSpawnPoints rightSpawnPoints = new TeamSpawnPoints();

    /// <summary>
    /// 指定した陣営の復活地点のワールド座標を取得する。
    /// </summary>
    public bool TryGetSpawnPosition(
        TeamSide side,
        out Vector2 position)
    {
        position = Vector2.zero;

        if (progressManager == null)
        {
            Debug.LogError(
                "RespawnPointManager: TeamProgressManagerが未設定です。"
            );
            return false;
        }

        TeamSpawnPoints spawnPoints =
            side == TeamSide.Left
                ? leftSpawnPoints
                : rightSpawnPoints;

        TeamProgressStage stage =
            progressManager.GetStage(side);

        int fieldIndex =
            progressManager.GetCurrentFieldIndex(side);

        GameObject spawnObject = null;

        switch (stage)
        {
            case TeamProgressStage.InitialCenter:
                spawnObject = spawnPoints.initialCenter;
                break;

            case TeamProgressStage.HomeEdgeAfterDeath:
                spawnObject = spawnPoints.centerFieldHomeEdge;
                break;

            case TeamProgressStage.GateBefore:
                spawnObject = GetFieldPoint(
                    spawnPoints.gateBeforeByField,
                    fieldIndex
                );
                break;

            case TeamProgressStage.CastleFront:
                spawnObject = GetFieldPoint(
                    spawnPoints.castleFrontByField,
                    fieldIndex
                );
                break;
        }

        if (spawnObject == null)
        {
            Debug.LogError(
                $"復活地点のGameObjectが未設定です。" +
                $" 陣営={side}, 段階={stage}, " +
                $"フィールド={fieldIndex}",
                this
            );

            return false;
        }

        // 空オブジェクトのワールド座標を取得する。
        position = spawnObject.transform.position;

        return true;
    }

    private GameObject GetFieldPoint(
        GameObject[] points,
        int fieldIndex)
    {
        if (points == null ||
            fieldIndex < 0 ||
            fieldIndex >= points.Length)
        {
            return null;
        }

        return points[fieldIndex];
    }
}
