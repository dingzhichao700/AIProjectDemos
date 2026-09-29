using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 飞机死亡表现状态
/// </summary>
/// <remarks>
/// 记录单架飞机的爆炸解体或坠落表现进度和 View 收尾方式。
/// </remarks>
internal sealed class AircraftDeathPresentationViewState {

    /**飞机机身根节点*/
    public readonly RectTransform root;

    /**正在播放死亡表现的飞机*/
    public readonly AircraftVO aircraft;

    /**飞机死亡开始时使用的爆炸序列*/
    public readonly IReadOnlyList<AircraftExplosionVO> explosions;

    /**是否保留玩家机身供复活复用*/
    public readonly bool preserveRootForReuse;

    /**死亡表现所属计时器*/
    public readonly TimerType timerType;

    /**最后一段死亡特效开始回调*/
    public readonly Action lastExplosionStarted;

    /**全部死亡特效完成回调*/
    public readonly Action completed;

    /**仍在播放的一次性特效句柄*/
    public readonly List<FrameAnimationView> activeEffects = new List<FrameAnimationView>();

    /**坠落期间持续播放的冒烟特效*/
    public FrameAnimationView fallingSmokeEffect;

    /**爆炸序列已经推进的时间*/
    public float elapsed;

    /**下一项待播放的爆炸序号*/
    public int nextExplosionIndex;

    /**仍未完成的一次性特效数量*/
    public int activeExplosionCount;

    /**机身是否已经隐藏或回收*/
    public bool aircraftVisualRemoved;

    /**是否已经通知最后一段特效开始*/
    public bool lastExplosionNotified;

    /**是否已经启动两项坠地特效*/
    public bool landingEffectsStarted;

    /**本次表现是否已经取消*/
    public bool cancelled;

    public AircraftDeathPresentationViewState(RectTransform root, AircraftVO aircraft, IReadOnlyList<AircraftExplosionVO> explosions, bool preserveRootForReuse, TimerType timerType, Action lastExplosionStarted, Action completed) {
        this.root = root;
        this.aircraft = aircraft;
        this.explosions = explosions;
        this.preserveRootForReuse = preserveRootForReuse;
        this.timerType = timerType;
        this.lastExplosionStarted = lastExplosionStarted;
        this.completed = completed;
    }

}
