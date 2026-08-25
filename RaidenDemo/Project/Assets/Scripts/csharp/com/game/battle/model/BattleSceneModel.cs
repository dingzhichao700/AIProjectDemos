using System;
using System.Collections.Generic;

/// <summary>
/// 战斗场景数据管理
/// </summary>
/// <remarks>
/// 管理场景元素注册、唯一 ID、Timer 分组及场景时间订阅。
/// </remarks>
internal sealed class BattleSceneModel {

    private readonly Dictionary<long, SceneElementVO> elementMap =
        new Dictionary<long, SceneElementVO>();

    private readonly Dictionary<TimerType, List<SceneElementVO>> timerElementMap =
        new Dictionary<TimerType, List<SceneElementVO>> {
            { TimerType.SCENE, new List<SceneElementVO>() },
            { TimerType.PLAYER, new List<SceneElementVO>() },
            { TimerType.ENEMY, new List<SceneElementVO>() },
        };

    private long nextElementId = 1;
    private bool timeFlowStarted;
    private Action<float> sceneUpdate;
    private Action<float> playerUpdate;
    private Action<float> enemyUpdate;

    public IReadOnlyDictionary<long, SceneElementVO> elements => elementMap;

    /**分配本局唯一的场景元素 ID。*/
    public long CreateElementId() {
        return nextElementId++;
    }

    /**登记元素并校验阵营与场景 Timer 的对应关系。*/
    public void AddElement(SceneElementVO element) {
        if (element == null) {
            throw new ArgumentNullException(nameof(element));
        }
        if (!timerElementMap.TryGetValue(element.timerType,
            out List<SceneElementVO> timerElements)) {
            throw new InvalidOperationException(
                $"战斗场景元素 {element.id} 使用了非场景 Timer：{element.timerType}");
        }
        TimerType expectedTimer = element.faction == SceneElementFaction.PLAYER
            ? TimerType.PLAYER
            : element.faction == SceneElementFaction.ENEMY
                ? TimerType.ENEMY
                : TimerType.SCENE;
        if (element.timerType != expectedTimer) {
            throw new InvalidOperationException(
                $"场景元素 {element.id} 的阵营 {element.faction} 与 Timer {element.timerType} 不匹配");
        }
        if (elementMap.ContainsKey(element.id)) {
            throw new InvalidOperationException($"战斗场景元素 id 重复：{element.id}");
        }
        elementMap.Add(element.id, element);
        timerElements.Add(element);
    }

    /**移除元素并结束其逻辑生命周期。*/
    public bool RemoveElement(long id) {
        if (!elementMap.TryGetValue(id, out SceneElementVO element)) {
            return false;
        }
        elementMap.Remove(id);
        timerElementMap[element.timerType].Remove(element);
        element.Destroy();
        return true;
    }

    /**推进指定 Timer 分组中的全部元素。*/
    public void UpdateElements(TimerType timerType, float deltaTime) {
        List<SceneElementVO> timerElements = timerElementMap[timerType];
        for (int i = timerElements.Count - 1; i >= 0; i--) {
            SceneElementVO element = timerElements[i];
            if (element.destroyed) {
                elementMap.Remove(element.id);
                timerElements.RemoveAt(i);
                continue;
            }
            element.OnTimeUpdate(deltaTime);
        }
    }

    /**订阅三类场景 Timer，并分别转发给战斗业务入口。*/
    public void StartTimeFlow(Action<float> onSceneUpdate,
        Action<float> onPlayerUpdate, Action<float> onEnemyUpdate) {
        if (timeFlowStarted) {
            return;
        }
        sceneUpdate = onSceneUpdate;
        playerUpdate = onPlayerUpdate;
        enemyUpdate = onEnemyUpdate;
        timeFlowStarted = true;
        RookieEngine.sceneTimer.AddUpdateListener(this, sceneUpdate);
        RookieEngine.playerTimer.AddUpdateListener(this, playerUpdate);
        RookieEngine.enemyTimer.AddUpdateListener(this, enemyUpdate);
    }

    /**解除三类场景 Timer 订阅。*/
    public void StopTimeFlow() {
        if (!timeFlowStarted) {
            return;
        }
        RookieEngine.sceneTimer.RemoveUpdateListener(this, sceneUpdate);
        RookieEngine.playerTimer.RemoveUpdateListener(this, playerUpdate);
        RookieEngine.enemyTimer.RemoveUpdateListener(this, enemyUpdate);
        timeFlowStarted = false;
        sceneUpdate = null;
        playerUpdate = null;
        enemyUpdate = null;
    }

    /**销毁所有场景元素并重置 ID。*/
    public void Clear() {
        foreach (SceneElementVO element in elementMap.Values) {
            element.Destroy();
        }
        elementMap.Clear();
        foreach (List<SceneElementVO> timerElements in timerElementMap.Values) {
            timerElements.Clear();
        }
        nextElementId = 1;
    }
}
