using System;
using System.Collections;
using UnityEngine;

/// <summary>死亡停止・消失・復活・復活後無敵の順番を管理する。</summary>
public class PlayerDeathRespawnManager : MonoBehaviour
{
    [Header("地点管理")]
    [SerializeField] private RespawnPointManager respawnPointManager;
    [Header("時間（秒）")]
    [SerializeField, Min(0f)] private float deathHoldSeconds = 1f;
    [SerializeField, Min(0f)] private float vanishSeconds = 1f;
    [SerializeField, Min(0f)] private float invincibleSeconds = 2f;

    public event Action<TeamSide> Respawned;

    private readonly SeatRuntime left = new SeatRuntime();
    private readonly SeatRuntime right = new SeatRuntime();

    private sealed class SeatRuntime
    {
        public PlayerController player;
        public Vector2 initialPosition;
        public bool hasInitialPosition;
        public Coroutine routine;
        public Renderer[] renderers;
        public bool[] rendererEnabled;
        public bool originalSimulated = true;
        public bool sequenceActive;
    }

    private SeatRuntime GetSeat(TeamSide side) => side == TeamSide.Left ? left : right;

    public void RegisterPlayer(TeamSide side, PlayerController player)
    {
        if (player == null) return;
        SeatRuntime seat = GetSeat(side);
        seat.player = player;
        seat.initialPosition = player.transform.position;
        seat.hasInitialPosition = true;
    }

    public bool IsDeadOrVanished(TeamSide side) => GetSeat(side).sequenceActive;

    public void BeginDeathSequence(TeamSide side, PlayerController player)
    {
        if (player == null) return;
        SeatRuntime seat = GetSeat(side);
        if (seat.sequenceActive) return;
        if (seat.player != player) RegisterPlayer(side, player);
        seat.sequenceActive = true;
        CaptureVisualAndPhysics(seat, player);
        seat.routine = StartCoroutine(DeathRoutine(side, seat));
    }

    private void CaptureVisualAndPhysics(SeatRuntime seat, PlayerController player)
    {
        seat.renderers = player.GetComponentsInChildren<Renderer>(true);
        seat.rendererEnabled = new bool[seat.renderers.Length];
        for (int i = 0; i < seat.renderers.Length; i++)
            seat.rendererEnabled[i] = seat.renderers[i] != null && seat.renderers[i].enabled;
        seat.originalSimulated = player.Body != null && player.Body.simulated;
    }

    private IEnumerator DeathRoutine(TeamSide side, SeatRuntime seat)
    {
        if (deathHoldSeconds > 0f) yield return new WaitForSeconds(deathHoldSeconds);
        PlayerController player = seat.player;
        if (player == null) { ClearSequence(seat); yield break; }

        SetRenderersVisible(seat, false);
        if (player.Body != null)
        {
            player.Body.linearVelocity = Vector2.zero;
            player.Body.simulated = false;
        }
        if (vanishSeconds > 0f) yield return new WaitForSeconds(vanishSeconds);

        if (player == null) { ClearSequence(seat); yield break; }
        // 現在位置で初期化して、未代入エラーを防ぐ
        Vector2 spawnPosition = player.transform.position;
        bool gotSpawn = respawnPointManager != null &&
            respawnPointManager.TryGetSpawnPosition(side, out spawnPosition);
        if (!gotSpawn)
        {
            if (!seat.hasInitialPosition) seat.initialPosition = player.transform.position;
            spawnPosition = seat.initialPosition;
            Debug.LogWarning($"PlayerDeathRespawnManager：{side}の復活地点を取得できないため、初期位置へ復活します。", this);
        }

        if (player.Body != null) player.Body.simulated = true;
        SetRenderersVisible(seat, true);
        player.Revive(spawnPosition);
        player.IsInvincible = invincibleSeconds > 0f;
        seat.sequenceActive = false;
        seat.routine = null;
        Respawned?.Invoke(side);

        if (invincibleSeconds > 0f)
        {
            yield return new WaitForSeconds(invincibleSeconds);
            if (player != null) player.IsInvincible = false;
        }
        seat.renderers = null;
        seat.rendererEnabled = null;
    }

    private void SetRenderersVisible(SeatRuntime seat, bool visible)
    {
        if (seat.renderers == null) return;
        for (int i = 0; i < seat.renderers.Length; i++)
        {
            if (seat.renderers[i] == null) continue;
            seat.renderers[i].enabled = visible ? seat.rendererEnabled[i] : false;
        }
    }

    private void RestoreSeat(SeatRuntime seat)
    {
        if (seat.routine != null)
        {
            StopCoroutine(seat.routine);
            seat.routine = null;
        }
        if (seat.player != null)
        {
            if (seat.player.Body != null)
            {
                seat.player.Body.simulated = seat.originalSimulated;
                seat.player.Body.linearVelocity = Vector2.zero;
            }
            seat.player.IsInvincible = false;
        }
        SetRenderersVisible(seat, true);
        seat.sequenceActive = false;
        seat.renderers = null;
        seat.rendererEnabled = null;
    }

    private void ClearSequence(SeatRuntime seat)
    {
        seat.routine = null;
        seat.sequenceActive = false;
    }

    public void CancelAll()
    {
        RestoreSeat(left);
        RestoreSeat(right);
    }

    private void OnDisable() => CancelAll();
}
