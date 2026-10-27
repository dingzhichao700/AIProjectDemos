using System;
using System.Collections.Generic;

/// <summary>
/// 管理场景逻辑元素，并将不同 Timer 的时间更新分发给所属元素。
/// </summary>
public class SceneModel {

    private readonly Dictionary<long, SceneElementVO> elementMap =
        new Dictionary<long, SceneElementVO>();

    private readonly Dictionary<TimerType, List<SceneElementVO>> timerElementMap =
        new Dictionary<TimerType, List<SceneElementVO>> {
            { TimerType.SCENE, new List<SceneElementVO>() },
            { TimerType.PLAYER, new List<SceneElementVO>() },
            { TimerType.ENEMY, new List<SceneElementVO>() },
        };

    private bool timeFlowStarted;

    public event Action<float> sceneTimeUpdated;

    public event Action<float> playerTimeUpdated;

    public event Action<float> enemyTimeUpdated;

    public IReadOnlyDictionary<long, SceneElementVO> elements => elementMap;

    /**订阅三类场景计时器，开始推进场景逻辑*/
    public void StartTimeFlow() {
        if (timeFlowStarted) {
            return;
        }
        timeFlowStarted = true;
        RookieEngine.sceneTimer.AddUpdateListener(this, OnSceneTimeUpdate);
        RookieEngine.playerTimer.AddUpdateListener(this, OnPlayerTimeUpdate);
        RookieEngine.enemyTimer.AddUpdateListener(this, OnEnemyTimeUpdate);
    }

    /**取消计时器订阅，停止推进场景逻辑*/
    public void StopTimeFlow() {
        if (!timeFlowStarted) {
            return;
        }
        timeFlowStarted = false;
        RookieEngine.sceneTimer.RemoveUpdateListener(this, OnSceneTimeUpdate);
        RookieEngine.playerTimer.RemoveUpdateListener(this, OnPlayerTimeUpdate);
        RookieEngine.enemyTimer.RemoveUpdateListener(this, OnEnemyTimeUpdate);
    }

    /**登记场景元素，并按其 Timer 类型归类*/
    public void AddElement(SceneElementVO element) {
        if (element == null) {
            throw new ArgumentNullException(nameof(element));
        }
        if (!timerElementMap.TryGetValue(element.timerType, out List<SceneElementVO> elements)) {
            throw new InvalidOperationException(
                $"场景元素 {element.id} 使用了非场景 Timer：{element.timerType}");
        }
        if (elementMap.ContainsKey(element.id)) {
            throw new InvalidOperationException($"场景元素 id 重复：{element.id}");
        }
        elementMap.Add(element.id, element);
        elements.Add(element);
    }

    /**移除并销毁指定场景元素*/
    public bool RemoveElement(long id) {
        if (!elementMap.TryGetValue(id, out SceneElementVO element)) {
            return false;
        }
        elementMap.Remove(id);
        timerElementMap[element.timerType].Remove(element);
        element.Destroy();
        return true;
    }

    /**销毁并清空当前全部场景元素*/
    public void Clear() {
        foreach (SceneElementVO element in elementMap.Values) {
            element.Destroy();
        }
        elementMap.Clear();
        foreach (List<SceneElementVO> elements in timerElementMap.Values) {
            elements.Clear();
        }
    }

    /**推进使用 sceneTimer 的元素*/
    private void OnSceneTimeUpdate(float deltaTime) {
        UpdateElements(TimerType.SCENE, deltaTime);
        sceneTimeUpdated?.Invoke(deltaTime);
    }

    /**推进使用 playerTimer 的元素*/
    private void OnPlayerTimeUpdate(float deltaTime) {
        UpdateElements(TimerType.PLAYER, deltaTime);
        playerTimeUpdated?.Invoke(deltaTime);
    }

    /**推进使用 enemyTimer 的元素*/
    private void OnEnemyTimeUpdate(float deltaTime) {
        UpdateElements(TimerType.ENEMY, deltaTime);
        enemyTimeUpdated?.Invoke(deltaTime);
    }

    /**向指定 Timer 分组内的元素分发时间更新*/
    private void UpdateElements(TimerType timerType, float deltaTime) {
        List<SceneElementVO> elements = timerElementMap[timerType];
        for (int i = elements.Count - 1; i >= 0; i--) {
            SceneElementVO element = elements[i];
            if (element.destroyed) {
                elementMap.Remove(element.id);
                elements.RemoveAt(i);
                continue;
            }
            element.OnTimeUpdate(deltaTime);
        }
    }

}
