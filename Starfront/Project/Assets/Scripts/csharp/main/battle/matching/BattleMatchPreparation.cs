using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cfg;

/// <summary>执行本次冻结资源闭包；不发布模型，不启动战斗，不自行管理全局缓存。</summary>
internal static class BattleMatchPreparation
{
    private sealed class LoadItem
    {
        public ResLoadInfo Resource;
        public string ImageAddress;
        public readonly List<ImageResource> Images = new List<ImageResource>();
    }

    internal static async Task<BattlePreparedBattle> PrepareAsync(BattleControl owner, BattleMatchPairVO pair,
        Action<BattlePreparationProgressVO> report, CancellationToken cancellation)
    {
        BattleSceneModel candidate = null;
        var retained = new List<IDisposable>();
        var acquired = new HashSet<string>();
        void Check() => cancellation.ThrowIfCancellationRequested();
        try
        {
            Check();
            report?.Invoke(new BattlePreparationProgressVO(0, 0, "model", 0, true));
            await CfgManager.EnsureLoadedAsync();
            Check();
            candidate = new BattleSceneModel(CfgManager.tables.BattleSceneObj.Get(pair.SceneId),
                CfgManager.tables.BattleSceneCellObj.DataList);
            candidate.InitializeBattle(CfgManager.tables, playerDecks: pair.Decks);
            candidate.StopTimeFlow();
            ValidateSnapshot(candidate, pair);
            var loads = BuildPlan(candidate, pair);
            var downloads = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var item in loads)
            {
                string key = item.Resource.path;
                switch (item.Resource.resType)
                {
                    case ResType.FrameAnim:
                        downloads.Add(key + ".png");
                        downloads.Add(key + ".json");
                        break;
                    case ResType.Atlas: downloads.Add(key + ".png"); break;
                    case ResType.Material: downloads.Add(key + ".mat"); break;
                    case ResType.Json: downloads.Add(key + ".json"); break;
                    default: downloads.Add(key); break;
                }
            }
            int total = 2 + downloads.Count + loads.Count;
            int completed = 1;
            report?.Invoke(new BattlePreparationProgressVO(completed, total, "model", 1, true));
            foreach (string key in downloads)
            {
                Check();
                string id = "download:" + key;
                report?.Invoke(new BattlePreparationProgressVO(completed, total, id, 0, false));
                Check();
                await ResourceLoader.DownloadDependenciesAsync(key, value =>
                {
                    if (!cancellation.IsCancellationRequested)
                    {
                        report?.Invoke(new BattlePreparationProgressVO(completed, total, id, value, false));
                    }
                });
                Check();
                completed++;
                report?.Invoke(new BattlePreparationProgressVO(completed, total, id, 1, false));
            }
            foreach (var item in loads)
            {
                Check();
                string id = "load:" + item.Resource.resType + ":" + item.Resource.path;
                report?.Invoke(new BattlePreparationProgressVO(completed, total, id, 0, true));
                Check();
                if (item.ImageAddress != null)
                {
                    // 共享atlas及散图唯一物理持有者为现有CultivationImageLease，每个资源键只租一次。
                    var lease = await CultivationImageLease.RetainImageAsync(item.ImageAddress);
                    retained.Add(lease);
                    Check();
                    foreach (var image in item.Images)
                    {
                        var sprite = image.Type == ResType.Atlas
                            ? ResourceManager.GetAtlasSprite(image.Address, image.SpriteName)
                            : ResourceManager.GetUnpackImage(image.Address);
                        if (sprite == null)
                        {
                            throw new InvalidOperationException("准备后图片缺失：" + image.Address + "/" + image.SpriteName);
                        }
                    }
                }
                else
                {
                    bool resident = item.Resource.resType == ResType.Material
                        ? ResourceManager.GetMaterial(item.Resource.path) != null
                        : item.Resource.resType == ResType.Prefab
                            ? ResourceManager.GetLoadedPrefab(item.Resource.path) != null
                            : true; // 帧动画使用既有FrameAnimationManager缓存，禁止另行释放其底层图。
                    if (!resident)
                    {
                        acquired.Add(item.Resource.path);
                    }
                    await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { item.Resource });
                    Check();
                    if (item.Resource.resType == ResType.FrameAnim && !FrameAnimationManager.HasLoad(item.Resource.path))
                    {
                        throw new InvalidOperationException("帧动画未完成解码：" + item.Resource.path);
                    }
                }
                completed++;
                report?.Invoke(new BattlePreparationProgressVO(completed, total, id, 1, true));
            }
            Check();
            report?.Invoke(new BattlePreparationProgressVO(completed, total, "validate", 0, true));
            Check();
            ValidateSnapshot(candidate, pair);
            candidate.StopTimeFlow();
            report?.Invoke(new BattlePreparationProgressVO(++completed, total, "validate", 1, true));
            Check();
            return new BattlePreparedBattle(owner, candidate, retained, acquired);
        }
        catch
        {
            candidate?.Dispose();
            foreach (var lease in retained)
            {
                lease.Dispose();
            }
            foreach (string path in acquired)
            {
                ResourceManager.Release(path);
            }
            throw;
        }
    }

    private static List<LoadItem> BuildPlan(BattleSceneModel model, BattleMatchPairVO pair)
    {
        var items = new Dictionary<string, LoadItem>(StringComparer.Ordinal);
        void AddImage(string address)
        {
            var image = ResourcePresentation.Policy.ResolveImage(address);
            string key = image.Type + ":" + image.Address;
            if (!items.TryGetValue(key, out var item))
            {
                item = new LoadItem { Resource = new ResLoadInfo(image.Address, image.Type), ImageAddress = address };
                items.Add(key, item);
            }
            if (!item.Images.Any(i => i.SpriteName == image.SpriteName))
            {
                item.Images.Add(image);
            }
        }
        void Add(string path, ResType type)
        {
            string key = type + ":" + path;
            if (!items.ContainsKey(key))
            {
                items.Add(key, new LoadItem { Resource = new ResLoadInfo(path, type) });
            }
        }
        foreach (string path in BattleSceneResources.Collect(model))
        {
            AddImage(path);
        }
        foreach (string path in BattleMatchResources.CollectDisplayImages(pair))
        {
            AddImage(path);
        }
        Add("Assets/Art/material/" + BattleConst.CoverageMaterialResource, ResType.Material);
        foreach (string path in BattleSceneResources.CollectAnimations(model))
        {
            Add(path, ResType.FrameAnim);
        }
        Add(ResourceConst.GetUIPath(MainUIEnum.BATTLE_SCENE_PANEL), ResType.Prefab);
        Add(ResourceConst.GetUIPath(MainUIEnum.BATTLE_INFO_PANEL), ResType.Prefab);
        return items.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Value).ToList();
    }

    private static void ValidateSnapshot(BattleSceneModel model, BattleMatchPairVO pair)
    {
        foreach (var camp in model.camps)
        {
            var display = camp.Key == BattleSceneCamp.FRIENDLY ? pair.Friendly : pair.Enemy;
            var actual = camp.Value.cardIds.Where(id => CfgManager.tables.CardObj.Get(id).Content is UnitCardContent).OrderBy(id => id);
            if (!actual.SequenceEqual(display.EffectiveCards.Select(c => c.CardId).OrderBy(id => id))
                || display.EffectiveCards.Any(c => !camp.Value.cardLevels.TryGetValue(c.CardId, out int level) || level != c.Level))
            {
                throw new InvalidOperationException("准备结果与匹配冻结卡组不一致，须重开匹配");
            }
        }
    }
}
