using System;
using UnityEngine;

/// <summary>完整图片资源描述；不依赖项目目录约定。</summary>
public sealed class ImageResource
{
    public string Address { get; }
    public string SpriteName { get; }
    public ResType Type { get; }

    public ImageResource(string address, ResType type, string spriteName = null)
    {
        if (string.IsNullOrEmpty(address) || (type != ResType.Atlas && type != ResType.UnpackImage) || (type == ResType.Atlas && string.IsNullOrEmpty(spriteName)))
        {
            throw new ArgumentException("Invalid image resource description");
        }
        Address = address;
        Type = type;
        SpriteName = spriteName;
    }
}

/// <summary>由已装载的业务模块提供资源约定；Foundation 不知道业务路径或 ID。</summary>
public interface IResourcePresentationPolicy
{
    string ResolveUI(string id);
    ImageResource ResolveImage(string id);
    string ResolveAudio(int id);
    int ResolveButtonSound(ButtonSoundType type, bool interactable);
    Material GetFrameMaterial();
}

public static class ResourcePresentation
{
    private static IResourcePresentationPolicy policy;
    public static IResourcePresentationPolicy Policy
    {
        get
        {
            if (policy == null)
            {
                throw new InvalidOperationException("Resource presentation policy must be installed before opening UI");
            }
            return policy;
        }
    }

    public static void Install(IResourcePresentationPolicy value)
    {
        policy = value ?? throw new ArgumentNullException(nameof(value));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        policy = null;
    }
}
