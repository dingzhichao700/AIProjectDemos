using System;
using System.Collections.Generic;
using cfg;

/// <summary>只收集场景可预测资源依赖，不执行加载或改变玩法数据。</summary>
public static class BattleSceneResources {
    public static string SpritePath(string resource) {
        if (string.IsNullOrWhiteSpace(resource) || resource.Contains("..") || resource.StartsWith("/") || resource.Contains(":")) {
            throw new InvalidOperationException("无效场景资源引用：" + resource);
        }
        return ResourceConst.PATH_UNPACK_IMAGE + resource + ".png";
    }

    public static string GroundMaterialPath(BattleSceneModel model) {
        string resource = model.config.GroundMaterialResource;
        if (string.IsNullOrWhiteSpace(resource) || resource.Contains("..") || resource.StartsWith("/") || resource.Contains(":")) {
            throw new InvalidOperationException("无效地表材质引用：" + resource);
        }
        return "Assets/Art/material/" + resource;
    }

    public static HashSet<string> Collect(BattleSceneModel model) {
        var result = new HashSet<string>();
        result.Add(SpritePath(model.config.GroundResource));
        result.Add(SpritePath(model.config.GroundGridResource));
        foreach (var item in model.config.Appearances) {
            if (item.ResourceType != BattleSceneResourceType.STATIC_IMAGE) {
                throw new NotSupportedException("场景帧动画尚未接入：" + item.Resource);
            }
            if (model.GetCell(item.Anchor.Q, item.Anchor.K) == null) {
                throw new InvalidOperationException("外观锚点格子不存在：" + item.Resource);
            }
            foreach (var coord in item.Cells) {
                if (model.GetCell(coord.Q, coord.K) == null) {
                    throw new InvalidOperationException("外观关联格子不存在：" + item.Resource);
                }
            }
            result.Add(SpritePath(item.Resource));
        }
        foreach (var building in PredictableBuildings(model)) {
            bool hasMain = false;
            foreach (var appearance in building.Appearances) {
                if (appearance.Direction != BuildingAppearanceDirection.FIXED || appearance.ResourceType != BattleSceneResourceType.STATIC_IMAGE) {
                    throw new NotSupportedException("首版建筑仅支持固定朝向静态外观：" + building.Id);
                }
                hasMain |= appearance.Part == BuildingAppearancePart.MAIN;
                result.Add(SpritePath(appearance.Resource));
            }
            if (!hasMain) {
                throw new InvalidOperationException("建筑缺少主体外观：" + building.Id);
            }
        }
        foreach (var owner in model.camps.Values) {
            foreach (int id in owner.cardIds) {
                result.Add(SpritePath(CfgManager.tables.CardObj.Get(id).Image));
            }
        }
        if (BattleConst.UnlockPulseSeconds <= 0 || BattleConst.UnlockMinAlpha < 0 || BattleConst.UnlockMinAlpha > 1 || BattleConst.InteractionHintSeconds <= 0) {
            throw new InvalidOperationException("解锁表现常量非法。");
        }
        foreach (var rule in CfgManager.tables.CellUnlockObj.DataList) {
            result.Add(SpritePath(BattleConst.UnlockIconResource(rule.Id)));
        }
        result.Add(SpritePath(BattleConst.UnlockGlowResource));
        result.Add(SpritePath(BattleConst.UnlockCurrencyResource));
        result.Add(SpritePath(BattleConst.ProductionBackgroundResource));
        result.Add(SpritePath(BattleConst.ProductionFillResource));
        return result;
    }

    private static IEnumerable<cfg.resource.BuildingResource> PredictableBuildings(BattleSceneModel model) {
        var ids = new HashSet<int>();
        foreach (var owner in model.camps.Values) {
            ids.Add(owner.config.BaseBuildingId);
            foreach (int id in owner.cardIds) {
                var content = CfgManager.tables.CardObj.Get(id).Content;
                if (content is BuildingCardContent building) {
                    ids.Add(building.BuildingId);
                } else if (content is UnitCardContent unit) {
                    ids.Add(CfgManager.tables.UnitProductionObj.Get(unit.UnitId).BuildingId);
                }
            }
        }
        foreach (int id in ids) {
            yield return CfgManager.tables.BuildingObj.Get(id);
        }
    }

    public static HashSet<string> CollectAnimations(BattleSceneModel model) {
        var result = new HashSet<string>();
        foreach (var owner in model.camps.Values) {
            foreach (int id in owner.cardIds) {
                if (!(CfgManager.tables.CardObj.Get(id).Content is UnitCardContent card)) {
                    continue;
                }
                foreach (var appearance in CfgManager.tables.UnitObj.Get(card.UnitId).Appearances) {
                    // 与静态资源共用相同的相对路径检查，帧动画交由现有共享缓存持有。
                    SpritePath(appearance.Resource);
                    result.Add(ResourceConst.GetFrameAnimationPath(appearance.Resource));
                }
            }
        }
        return result;
    }

}
