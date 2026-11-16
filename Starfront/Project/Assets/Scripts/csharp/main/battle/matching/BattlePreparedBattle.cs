using System;
using System.Collections.Generic;

/// <summary>准备请求独占的结果；领取前不发布模型，领取后由BattleControl和战斗页拥有。</summary>
public sealed class BattlePreparedBattle : IBattleMatchPreparedBattle
{
    private readonly BattleControl owner;
    internal BattleSceneModel Candidate;
    internal readonly List<IDisposable> Images;
    internal readonly HashSet<string> OwnedResources;
    private BattleSceneModel committedModel;
    private bool disposed;
    private bool transferred;

    internal BattlePreparedBattle(BattleControl owner, BattleSceneModel candidate,
        List<IDisposable> images, HashSet<string> resources)
    {
        this.owner = owner;
        Candidate = candidate;
        Images = images;
        OwnedResources = resources;
    }

    public bool TryCommit()
    {
        if (disposed || transferred || !owner.CommitPrepared(this))
        {
            return false;
        }
        transferred = true;
        committedModel = owner.model;
        return true;
    }

    public void AbortEntry()
    {
        if (transferred && ReferenceEquals(owner.model, committedModel))
        {
            owner.End();
        }
        committedModel = null;
    }

    public void Dispose()
    {
        if (disposed || transferred)
        {
            return;
        }
        disposed = true;
        owner.DetachPrepared(this);
        Candidate?.Dispose();
        Candidate = null;
        foreach (var image in Images)
        {
            image.Dispose();
        }
        Images.Clear();
        foreach (string path in OwnedResources)
        {
            ResourceManager.Release(path);
        }
        OwnedResources.Clear();
    }
}
