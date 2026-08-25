using System;
using UnityEngine;

/// <summary>
/// 玩家编队数据管理
/// </summary>
/// <remarks>
/// 管理玩家主机、僚机编队、玩家碰撞形状和本局剩余生命。
/// </remarks>
internal sealed class BattleFormationModel {

    private int persistentWingmanLevel = 1;

    public AircraftVO player { get; private set; }
    public AircraftVO leftWingman { get; private set; }
    public AircraftVO rightWingman { get; private set; }
    public AircraftCollisionVO playerCollision { get; private set; }
    public int playerLives { get; private set; }
    public bool isPlayerAlive => player != null && player.lifecycleState == PlayerLifecycleState.Alive;

    public void Initialize() {
        playerLives = BattleConst.PlayerInitialLifeCount;
        persistentWingmanLevel = 1;
    }

    public void SetPlayer(AircraftVO unit) {
        player = unit;
    }

    public AircraftVO ApplyWingmanReward(Func<string, bool, Vector2, AircraftVO> createUnit, out bool created, out bool isLeft) {
        created = false;
        isLeft = false;
        if (leftWingman == null) {
            isLeft = true;
            leftWingman = createUnit("wingmanLeftEntity", false, player.position + BattleConst.WingmanLeftOffset);
            leftWingman.ApplyLevels(persistentWingmanLevel, 0);
            created = true;
        } else if (rightWingman == null) {
            rightWingman = createUnit("wingmanRightEntity", false, player.position + BattleConst.WingmanRightOffset);
            rightWingman.ApplyLevels(persistentWingmanLevel, 0);
            created = true;
        } else {
            AircraftVO target = leftWingman.stageBonusLevel <= rightWingman.stageBonusLevel ? leftWingman : rightWingman;
            target.ApplyLevels(target.persistentLevel, target.stageBonusLevel + 1);
            return target;
        }
        ConfigureFollowers();
        return isLeft ? leftWingman : rightWingman;
    }

    public void SetCollision(AircraftCollisionVO collision) {
        playerCollision = collision;
    }

    public void ApplyPersistentLevels(int playerLevel, int wingmanLevel) {
        player?.ApplyLevels(playerLevel, player.stageBonusLevel);
        persistentWingmanLevel = Mathf.Max(1, wingmanLevel);
        leftWingman?.ApplyLevels(wingmanLevel, leftWingman.stageBonusLevel);
        rightWingman?.ApplyLevels(wingmanLevel, rightWingman.stageBonusLevel);
    }

    public void ApplyStageBonus(int playerBonusLevel, int wingmanBonusLevel) {
        if (player != null) {
            player.ApplyLevels(player.persistentLevel, playerBonusLevel);
        }
        if (leftWingman != null) {
            leftWingman.ApplyLevels(leftWingman.persistentLevel, wingmanBonusLevel);
        }
        if (rightWingman != null) {
            rightWingman.ApplyLevels(rightWingman.persistentLevel, wingmanBonusLevel);
        }
    }

    public int AddLife(int value) {
        playerLives += Mathf.Max(0, value);
        return playerLives;
    }

    public int ConsumeLife() {
        playerLives = Mathf.Max(0, playerLives - 1);
        return playerLives;
    }

    public void Clear() {
        player = null;
        leftWingman = null;
        rightWingman = null;
        playerCollision = null;
        Initialize();
    }

    private void ConfigureFollowers() {
        leftWingman?.ConfigureFollow(player, BattleConst.WingmanLeftOffset, BattleConst.WingmanFollowSpeed);
        rightWingman?.ConfigureFollow(player, BattleConst.WingmanRightOffset, BattleConst.WingmanFollowSpeed);
    }
}
