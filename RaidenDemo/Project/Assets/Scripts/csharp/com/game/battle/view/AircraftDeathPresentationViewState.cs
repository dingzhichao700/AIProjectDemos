using System;
using System.Collections.Generic;
using cfg;
using UnityEngine;

/// <summary>
/// 飞机死亡表现状态
/// </summary>
/// <remarks>
/// 记录单架飞机的爆炸播放进度和 View 收尾方式。
/// </remarks>
internal sealed class AircraftDeathPresentationViewState {

    public readonly RectTransform root;
    public readonly Vector2 position;
    public readonly IReadOnlyList<ExplosionEffect> explosions;
    public readonly bool removeAfterCompletion;
    public readonly bool preserveRootForReuse;
    public readonly TimerType timerType;
    public readonly Action completed;
    public float elapsed;
    public int nextExplosionIndex;
    public int activeExplosionCount;
    public bool aircraftVisualRemoved;

    public AircraftDeathPresentationViewState(RectTransform root, Vector2 position, IReadOnlyList<ExplosionEffect> explosions, bool removeAfterCompletion, bool preserveRootForReuse, TimerType timerType, Action completed) {
        this.root = root;
        this.position = position;
        this.explosions = explosions;
        this.removeAfterCompletion = removeAfterCompletion;
        this.preserveRootForReuse = preserveRootForReuse;
        this.timerType = timerType;
        this.completed = completed;
    }
}
