using System;
using UnityEngine;

/// <summary>项目资源约定随 Login 更新；旧字符串图片接口在这里转换为明确描述。</summary>
public sealed class GameResourcePresentationPolicy : IResourcePresentationPolicy
{
    public GameResourcePresentationPolicy()
    {
        Debug.Log("[ResourcePolicy] " + ResourceConst.PresentationRevision);
    }

    public string ResolveUI(string id)
    {
        return ResourceConst.GetUIPath(id);
    }

    public ImageResource ResolveImage(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentException("Image resource ID is empty");
        }
        if (id.StartsWith(ResourceConst.PATH_ATLAS, StringComparison.Ordinal))
        {
            int separator = id.LastIndexOf('/');
            return new ImageResource(id.Substring(0, separator), ResType.Atlas, id.Substring(separator + 1));
        }
        if (id.StartsWith(ResourceConst.PATH_UNPACK_IMAGE, StringComparison.Ordinal))
        {
            return new ImageResource(id, ResType.UnpackImage);
        }
        throw new ArgumentException("Unknown project image resource: " + id);
    }

    public string ResolveAudio(int id)
    {
        return ResourceConst.GetAudioPathById(id);
    }

    public int ResolveButtonSound(ButtonSoundType type, bool interactable)
    {
        if (type == ButtonSoundType.None)
        {
            return 0;
        }
        if (!interactable)
        {
            return AudioConst.SOUND_CLICK_DISABLE_1;
        }
        switch (type)
        {
            case ButtonSoundType.DEFAULT_CLICK: return AudioConst.SOUND_CLICK_ENABLE_1;
            case ButtonSoundType.DEFAULT_HOVER: return AudioConst.SOUND_HOVER_1;
            case ButtonSoundType.ENSURE: return AudioConst.SOUND_CLICK_ENABLE_2;
            case ButtonSoundType.CANCEL: return AudioConst.SOUND_CLICK_ENABLE_3;
            default: return 0;
        }
    }

    public Material GetFrameMaterial()
    {
        return ResourceManager.GetMaterial(ResourceConst.PATH_MATERIAL + "custom/matInstanceImage");
    }
}
