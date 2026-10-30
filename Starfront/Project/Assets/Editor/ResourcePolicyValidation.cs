using System;
using UnityEngine;

/// <summary>在正式入口回归中验证资源策略所属程序集及替换契约。</summary>
public static class ResourcePolicyValidation
{
    private sealed class AlternatePolicy : IResourcePresentationPolicy
    {
        public string ResolveUI(string id) => "test/ui/" + id;
        public ImageResource ResolveImage(string id) => new ImageResource("test/images/" + id, ResType.UnpackImage);
        public string ResolveAudio(int id) => "test/audio/" + id;
        public int ResolveButtonSound(ButtonSoundType type, bool interactable) => interactable ? 42 : 43;
        public Material GetFrameMaterial() => null;
    }

    public static void Validate()
    {
        Require(typeof(ResourceConst).Assembly.GetName().Name == "Login", "resource paths in Login");
        Require(typeof(AudioConst).Assembly.GetName().Name == "Login", "audio mapping in Login");
        Require(typeof(UIEnum).Assembly.GetName().Name == "Login", "startup UI IDs in Login");
        Require(typeof(MainUIEnum).Assembly.GetName().Name == "Main", "game UI IDs in Main");
        Require(typeof(ResourcePresentation).Assembly.GetName().Name == "Foundation", "stable contract in Foundation");
        foreach (var reference in typeof(ResourcePresentation).Assembly.GetReferencedAssemblies())
        {
            Require(reference.Name != "Login" && reference.Name != "Main", "no reverse assembly dependency");
        }
        var original = ResourcePresentation.Policy;
        var atlas = original.ResolveImage(ResourceConst.PATH_ATLAS_TITLE + "sliderDot_red");
        Require(atlas.Type == ResType.Atlas && atlas.SpriteName == "sliderDot_red" && atlas.Address == ResourceConst.PATH_ATLAS_TITLE.TrimEnd('/'), "legacy atlas mapping");
        var loose = original.ResolveImage(ResourceConst.PATH_UNPACK_IMAGE + "example.png");
        Require(loose.Type == ResType.UnpackImage, "legacy loose image mapping");
        Require(original.ResolveUI(UIEnum.MAIN_PANEL) == ResourceConst.GetUIPath(UIEnum.MAIN_PANEL), "existing main address");
        Require(original.ResolveButtonSound(ButtonSoundType.None, false) == 0, "explicit silent button");
        bool rejected = false;
        try
        {
            new ImageResource("test/atlas", ResType.Atlas);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }
        Require(rejected, "atlas requires sprite name");
        try
        {
            ResourcePresentation.Install(new AlternatePolicy());
            Require(ResourcePresentation.Policy.ResolveUI("new-panel") == "test/ui/new-panel", "UI path can change without Foundation edit");
            Require(ResourcePresentation.Policy.ResolveImage("new-image").Address == "test/images/new-image", "image outside conventional root");
            Require(ResourcePresentation.Policy.ResolveAudio(9001) == "test/audio/9001", "audio mapping replaceable");
            Require(ResourcePresentation.Policy.ResolveButtonSound(ButtonSoundType.ENSURE, true) == 42, "button mapping replaceable");
            Require(ResourcePresentation.Policy.GetFrameMaterial() == null, "UI default material supported");
        }
        finally
        {
            ResourcePresentation.Install(original);
        }
        Debug.Log("[ResourcePolicyValidation] passed: assembly ownership, injected replacement, legacy addresses, explicit image kinds");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
