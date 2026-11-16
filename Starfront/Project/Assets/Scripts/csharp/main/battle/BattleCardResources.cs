using System;
using System.Collections.Generic;
using System.Linq;
using cfg;
using cfg.resource;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>只检查单卡本局实际消费的资源依赖；不加载、不改牌库资格或存档。</summary>
public static class BattleCardResources
{
    public static string UnavailableReason(Tables tables, int cardId, int level, Func<ResLoadInfo, bool> exists = null)
    {
        var card = tables.CardObj.GetOrDefault(cardId);
        if (card == null || !card.ShowInLibrary || level <= 0 || level > card.MaxLevel)
        {
            return "资格：卡牌不存在、未激活或等级无效";
        }
        if (!card.BattleEnabled || !(card.Content is UnitCardContent content))
        {
            return "配置：该卡尚未接通战斗内容";
        }
        var unit = tables.UnitObj.GetOrDefault(content.UnitId);
        var production = tables.UnitProductionObj.GetOrDefault(content.UnitId);
        var building = production == null ? null : tables.BuildingObj.GetOrDefault(production.BuildingId);
        if (unit == null || production == null || building == null)
        {
            return "配置：缺少单位或生产建筑关联";
        }
        var growth = tables.CardUnitLevelObj.DataList.SingleOrDefault(row => row.CardId == cardId && row.Level == level && row.UnitId == unit.Id);
        var buildingGrowth = tables.CardBuildingLevelObj.DataList.SingleOrDefault(row => row.CardId == cardId && row.Level == level && row.BuildingId == building.Id);
        if (growth == null || buildingGrowth == null)
        {
            return "配置：缺少本级战斗参数";
        }
        var requests = new List<ResLoadInfo>();
        if (string.IsNullOrWhiteSpace(card.Image))
        {
            return "资源：缺少战场揭示图引用";
        }
        requests.Add(new ResLoadInfo(BattleSceneResources.SpritePath(card.Image), ResType.UnpackImage));
        if (building.Appearances.Count == 0 || !building.Appearances.Any(a => a.Part == BuildingAppearancePart.MAIN))
        {
            return "资源：缺少生产建筑外观";
        }
        foreach (var appearance in building.Appearances)
        {
            if (string.IsNullOrWhiteSpace(appearance.Resource))
            {
                return "资源：生产建筑外观引用为空";
            }
            requests.Add(new ResLoadInfo(BattleSceneResources.SpritePath(appearance.Resource), ResType.UnpackImage));
        }
        if (growth.SkillIds.Count == 0)
        {
            return "配置：没有可执行战斗技能";
        }
        var skillRows = growth.SkillIds.Select(id => tables.SkillObj.GetOrDefault(id)).ToArray();
        if (skillRows.Any(row => row == null))
        {
            return "配置：战斗技能引用不存在";
        }
        string animationError = CollectRequiredUnitAnimations(unit, skillRows.Select(row => row.Action), requests);
        if (!string.IsNullOrEmpty(animationError))
        {
            return animationError;
        }
        var visual = tables.UnitVisualObj.GetOrDefault(unit.VisualId);
        if (visual == null)
        {
            return "配置：单位形象参数未接通";
        }
        if (!string.IsNullOrWhiteSpace(visual.DeathEffect))
        {
            requests.Add(new ResLoadInfo(ResourceConst.GetFrameAnimationPath(visual.DeathEffect), ResType.FrameAnim));
        }
        foreach (var skill in skillRows)
        {
            foreach (var effect in skill.Effects.OfType<LaunchProjectileEffect>())
            {
                var projectile = tables.ProjectileObj.GetOrDefault(effect.ProjectileId);
                if (projectile == null)
                {
                    return "配置：弹道引用不存在";
                }
                if (string.IsNullOrWhiteSpace(projectile.Resource) || string.IsNullOrWhiteSpace(projectile.ImpactResource))
                {
                    return "资源：缺少弹体或命中特效引用";
                }
                requests.Add(new ResLoadInfo(BattleSceneResources.SpritePath(projectile.Resource), ResType.UnpackImage));
                requests.Add(new ResLoadInfo(ResourceConst.GetFrameAnimationPath(projectile.ImpactResource), ResType.FrameAnim));
            }
        }
        exists = exists ?? HasResource;
        foreach (var request in requests.GroupBy(r => (r.path, r.resType)).Select(g => g.First()))
        {
            if (!exists(request))
            {
                return "资源：未登记或缺少 " + request.path;
            }
        }
        return string.Empty;
    }

    /// <summary>准入和预加载使用相同的本级技能动作、基础动作及两源方向集合。</summary>
    public static string CollectRequiredUnitAnimations(UnitResource unit, IEnumerable<UnitActionType> skillActions, ICollection<ResLoadInfo> requests)
    {
        if (unit == null || skillActions == null || requests == null)
        {
            throw new ArgumentNullException("Unit animation dependency inputs");
        }
        var required = skillActions.Concat(new[] { UnitActionType.MOVE, UnitActionType.IDLE, UnitActionType.DEATH }).Distinct();
        foreach (var action in required)
        {
            foreach (var direction in BattleAim.SourceDirections)
            {
                var appearance = unit.Appearances.SingleOrDefault(item => item.Action == action && item.Direction == direction);
                if (appearance == null || string.IsNullOrWhiteSpace(appearance.Resource))
                {
                    return "资源：缺少单位动作 " + action + "/" + direction;
                }
                BattleSceneResources.SpritePath(appearance.Resource);
                requests.Add(new ResLoadInfo(ResourceConst.GetFrameAnimationPath(appearance.Resource), ResType.FrameAnim));
            }
        }
        return string.Empty;
    }

    private static bool HasResource(ResLoadInfo request)
    {
        if (request.resType == ResType.FrameAnim)
        {
            return FrameAnimationManager.GetRes(request.path) != null
                || (HasLocation(request.path + ".png", typeof(Sprite)) && HasLocation(request.path + ".json", typeof(TextAsset)));
        }
        return ResourceManager.HasLoadedUnpackImage(request.path) || HasLocation(request.path, typeof(Sprite));
    }

    private static bool HasLocation(string address, Type type)
    {
        foreach (var locator in Addressables.ResourceLocators)
        {
            if (locator.Locate(address, type, out var locations) && locations != null && locations.Count > 0)
            {
                return true;
            }
        }
        return false;
    }
}
