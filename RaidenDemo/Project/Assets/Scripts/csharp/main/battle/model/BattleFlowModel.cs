using UnityEngine;

/// <summary>
/// 战斗流程数据管理
/// </summary>
/// <remarks>
/// 管理单局战斗流程状态、模拟开关和关卡进度。
/// </remarks>
internal sealed class BattleFlowModel {

    public BattleFlowState state { get; private set; }
    public float missionProgress { get; private set; } = 1f;
    public bool simulationActive { get; private set; }
    public bool isPlaying => state == BattleFlowState.Playing;

    /**重置为尚未开始的准备状态。*/
    public void Initialize() {
        state = BattleFlowState.Preparing;
        missionProgress = 1f;
        simulationActive = false;
    }

    /**设置是否允许 Timer 推进战斗模拟。*/
    public void SetSimulationActive(bool value) {
        simulationActive = value;
    }

    public bool Start() {
        if (state != BattleFlowState.Preparing) {
            return false;
        }
        state = BattleFlowState.Playing;
        simulationActive = true;
        return true;
    }

    public bool Pause() {
        if (state != BattleFlowState.Playing) {
            return false;
        }
        state = BattleFlowState.Paused;
        simulationActive = false;
        return true;
    }

    public bool Resume() {
        if (state != BattleFlowState.Paused) {
            return false;
        }
        state = BattleFlowState.Playing;
        simulationActive = true;
        return true;
    }

    public bool Settle() {
        if (state != BattleFlowState.Playing) {
            return false;
        }
        state = BattleFlowState.Settling;
        simulationActive = false;
        return true;
    }

    public void Close() {
        state = BattleFlowState.Closing;
        simulationActive = false;
    }

    public void SetMissionProgress(float value) {
        missionProgress = Mathf.Clamp01(value);
    }
}
