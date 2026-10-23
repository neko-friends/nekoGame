using UnityEngine;

[System.Serializable]
public class TeamSpawnPoints
{
    [Header("試合開始時：中央陣地の開始位置(企画書の矢印の位置)")]
    public GameObject initialCenter;

    [Header("初デス後：中央陣地の自陣側の端")]
    public GameObject centerFieldHomeEdge;

    [Header("各陣地の関門前：陣地0〜8")]
    public GameObject[] gateBeforeByField = new GameObject[MatchLayout.FieldCount];

    [Header("各陣地の城前(関門突破後)：陣地0〜8")]
    public GameObject[] castleFrontByField = new GameObject[MatchLayout.FieldCount];

    /// <summary>陣地が3→9になったので、古いシリアライズ済みデータの配列長を揃える。</summary>
    public void EnsureSize()
    {
        if (gateBeforeByField == null ||
            gateBeforeByField.Length != MatchLayout.FieldCount)
        {
            System.Array.Resize(ref gateBeforeByField, MatchLayout.FieldCount);
        }

        if (castleFrontByField == null ||
            castleFrontByField.Length != MatchLayout.FieldCount)
        {
            System.Array.Resize(ref castleFrontByField, MatchLayout.FieldCount);
        }
    }
}

public class RespawnPointManager : MonoBehaviour
{
    [Header("進行管理")]
    [SerializeField]
    private TeamProgressManager progressManager;

    [Header("陣地のルート(Field_0〜Field_8)。自動割り当てに使う")]
    [SerializeField]
    private Transform[] fieldRoots = new Transform[MatchLayout.FieldCount];

    [Header("仮戦闘シーン：左右固定の地点（Progress Manager未設定時に使用）")]
    [SerializeField] private GameObject leftRespawnPoint;
    [SerializeField] private GameObject rightRespawnPoint;
    [SerializeField] private GameObject leftStartPoint;
    [SerializeField] private GameObject rightStartPoint;

    [Header("旧9陣地版の地点（Progress Manager設定時に使用）")]
    [SerializeField]
    private TeamSpawnPoints leftSpawnPoints = new TeamSpawnPoints();

    [Header("右陣営の復活地点")]
    [SerializeField]
    private TeamSpawnPoints rightSpawnPoints = new TeamSpawnPoints();

    private void OnValidate()
    {
        leftSpawnPoints.EnsureSize();
        rightSpawnPoints.EnsureSize();

        if (fieldRoots == null || fieldRoots.Length != MatchLayout.FieldCount)
            System.Array.Resize(ref fieldRoots, MatchLayout.FieldCount);
    }

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
            GameObject fixedPoint = side == TeamSide.Left ? leftRespawnPoint : rightRespawnPoint;
            if (fixedPoint == null)
            {
                Debug.LogError($"RespawnPointManager：{side}の復活地点が未設定です。", this);
                return false;
            }
            position = fixedPoint.transform.position;
            return true;
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

                // その陣地に城前が置かれていなければ、関門前で代用する。
                if (spawnObject == null)
                {
                    spawnObject = GetFieldPoint(
                        spawnPoints.gateBeforeByField,
                        fieldIndex
                    );
                }
                break;
        }

        if (spawnObject == null)
        {
            Debug.LogError(
                $"復活地点のGameObjectが未設定です。" +
                $" 陣営={side}, 段階={stage}, " +
                $"陣地={fieldIndex}",
                this
            );

            return false;
        }

        // 空オブジェクトのワールド座標を取得する。
        position = spawnObject.transform.position;

        return true;
    }

    /// <summary>試合開始時の配置位置を取得する。開始地点未設定なら復活地点を使う。</summary>
    public bool TryGetStartPosition(TeamSide side, out Vector2 position)
    {
        position = Vector2.zero;
        if (progressManager != null)
        {
            TeamSpawnPoints spawnPoints = side == TeamSide.Left ? leftSpawnPoints : rightSpawnPoints;
            GameObject point = spawnPoints.initialCenter;
            if (point == null)
                return TryGetSpawnPosition(side, out position);
            position = point.transform.position;
            return true;
        }

        GameObject startPoint = side == TeamSide.Left ? leftStartPoint : rightStartPoint;
        if (startPoint == null)
            startPoint = side == TeamSide.Left ? leftRespawnPoint : rightRespawnPoint;
        if (startPoint == null)
        {
            Debug.LogError($"RespawnPointManager：{side}の開始地点・復活地点が未設定です。", this);
            return false;
        }
        position = startPoint.transform.position;
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

    // =================================================================
    // 自動割り当て(コンポーネントの右クリックメニューから実行)
    // 命名規則:
    //   Field_n/Spawn_{Left|Right}_GateBefore
    //   Field_n/Spawn_{Left|Right}_CastleFront
    //   中央陣地のみ Spawn_{Left|Right}_Initial / Spawn_{Left|Right}_HomeEdge
    // 見つかったものだけ上書きし、見つからないものは今の設定を残す。
    // =================================================================

    [ContextMenu("子オブジェクトの名前から復活地点を自動割り当て")]
    private void AutoAssignFromFieldRoots()
    {
        int count = AutoAssign(leftSpawnPoints, "Left")
                  + AutoAssign(rightSpawnPoints, "Right");

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif

        Debug.Log($"RespawnPointManager: 復活地点を{count}個割り当てました。", this);
    }

    private int AutoAssign(TeamSpawnPoints points, string sideName)
    {
        points.EnsureSize();
        int count = 0;

        Transform center = GetFieldRoot(MatchLayout.StartFieldIndex);
        if (AssignPoint(ref points.initialCenter, center, $"Spawn_{sideName}_Initial")) count++;
        if (AssignPoint(ref points.centerFieldHomeEdge, center, $"Spawn_{sideName}_HomeEdge")) count++;

        for (int i = 0; i < MatchLayout.FieldCount; i++)
        {
            Transform root = GetFieldRoot(i);
            if (AssignPoint(ref points.gateBeforeByField[i], root, $"Spawn_{sideName}_GateBefore")) count++;
            if (AssignPoint(ref points.castleFrontByField[i], root, $"Spawn_{sideName}_CastleFront")) count++;
        }

        return count;
    }

    private Transform GetFieldRoot(int index)
    {
        if (fieldRoots == null || index < 0 || index >= fieldRoots.Length) return null;
        return fieldRoots[index];
    }

    private static bool AssignPoint(ref GameObject target, Transform root, string childName)
    {
        if (root == null) return false;

        Transform child = root.Find(childName);
        if (child == null) return false;

        target = child.gameObject;
        return true;
    }
}
