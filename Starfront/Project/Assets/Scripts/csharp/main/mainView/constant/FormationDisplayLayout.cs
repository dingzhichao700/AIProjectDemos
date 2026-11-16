using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 阵容与队伍编辑共用的上阵立绘体量定标。
/// </summary>
/// <remarks>仅为已验收立绘的显示参数；不决定战斗体型或移动类型。</remarks>
public static class FormationDisplayLayout {

    // R5视觉返修：各档相对比例不变，统一提高展示基准。
    private const float displayScale = 1.10f;

    private readonly struct Profile {

        public readonly float ratio;
        public readonly Vector2 visibleFraction;

        public Profile(float ratio, Vector2 visibleFraction) {
            this.ratio = ratio;
            this.visibleFraction = visibleFraction;
        }

    }

    // R4已批准11卡及2026-11-11真人确认的剩余5卡，共16张Formation资源。可见包围盒Alpha>=32；更换美术须重新定标。
    private static readonly Dictionary<int, Profile> profiles = new Dictionary<int, Profile> {
        { 2101, new Profile(0.6f, new Vector2(0.852941176f, 0.931972789f)) }, // 陆战队员
        { 2102, new Profile(0.8f, new Vector2(0.959459459f, 0.741258741f)) }, // 医疗运输机
        { 2103, new Profile(0.8f, new Vector2(0.932203390f, 0.861818182f)) }, // 攻城坦克
        { 2104, new Profile(0.6f, new Vector2(0.940092166f, 0.937704918f)) }, // 幽灵
        { 2105, new Profile(1f, new Vector2(0.915492958f, 0.835164835f)) }, // 战列巡航舰
        { 2202, new Profile(0.7f, new Vector2(0.918604651f, 0.946428571f)) }, // 刺蛇
        { 2204, new Profile(0.5f, new Vector2(0.954385965f, 0.871900826f)) }, // 感染虫
        { 2205, new Profile(1f, new Vector2(0.983827493f, 0.924398625f)) }, // 雷兽
        { 2301, new Profile(0.7f, new Vector2(0.884615385f, 0.953974895f)) }, // 追猎者
        { 2303, new Profile(0.5f, new Vector2(0.885608856f, 0.882075472f)) }, // 哨兵
        { 2304, new Profile(0.8f, new Vector2(0.858108108f, 0.919678715f)) }, // 不朽者
        { 2201, new Profile(0.8f, new Vector2(0.956363636f, 0.884773663f)) }, // 蟑螂
        { 2203, new Profile(0.8f, new Vector2(0.934065934f, 0.938566553f)) }, // 异龙
        { 2302, new Profile(0.6f, new Vector2(0.915750916f, 0.928870293f)) }, // 狂热者
        { 2305, new Profile(1f, new Vector2(0.953846154f, 0.933054393f)) }, // 风暴战舰
        { 2306, new Profile(0.6f, new Vector2(0.910505837f, 0.925196850f)) }, // 高阶圣堂武士
    };

    /// <summary>
    /// 用座台实际宽度定标，保留Sprite语义锚点，避免两界面各自撑满立绘框。
    /// </summary>
    /// <returns>未定档单位返回false，由调用方保留其既有显示。</returns>
    public static bool TryApply(int cardId, Image artwork, Image stage, Vector2 stageAnchor) {
        if (!profiles.TryGetValue(cardId, out Profile profile) || artwork.sprite == null || stage == null) {
            return false;
        }
        var rect = artwork.rectTransform;
        var sprite = artwork.sprite;
        float stageWidth = stage.rectTransform.rect.width * Mathf.Abs(stage.transform.lossyScale.x / rect.parent.lossyScale.x);
        float visibleExtent = NativePortraitGeometry.TryGet(cardId, sprite, out var native)
            ? Mathf.Max(native.visible.x, native.visible.y)
            : Mathf.Max(sprite.rect.width * profile.visibleFraction.x, sprite.rect.height * profile.visibleFraction.y);
        if (stageWidth <= 0 || visibleExtent <= 0) {
            return false;
        }
        float scale = stageWidth * displayScale * profile.ratio / visibleExtent;
        rect.pivot = sprite.pivot / sprite.rect.size;
        rect.sizeDelta = sprite.rect.size * scale;
        rect.anchoredPosition = stageAnchor;
        AlignToStage(artwork, stage);
        return true;
    }

    /// <summary>按座台源图顶面中心定位，不用矩形中心或逐卡补偿。</summary>
    public static bool AlignToStage(Image artwork, Image stage) {
        if (!TryGetStageTopWorld(stage, out Vector3 world)) {
            return false;
        }
        artwork.rectTransform.position = world;
        return true;
    }

    /// <summary>源图语义点经过Image等比留白及父级变换，得到实际承托点。</summary>
    public static bool TryGetStageTopWorld(Image stage, out Vector3 world) {
        world = Vector3.zero;
        if (stage == null || stage.sprite == null) {
            return false;
        }
        // Split round podiums share one semantic support; legacy quality sprites select glow only.
        // Preserve the approved green/gold support: logical image top - 9.416389 at width 156.
        if (stage.GetComponent<FormationStageLayers>() != null) {
            Rect rect = stage.rectTransform.rect;
            world = stage.rectTransform.TransformPoint(new Vector3(rect.center.x,
                rect.yMax - 9.416389f * rect.width / 156f, 0f));
            return true;
        }
        Vector2 center;
        switch (stage.sprite.name) {
            case "podium-green":
            case "podium-purple":
            case "podium-gold":
                center = new Vector2(.5f, 1f - 125f / 453f);
                break;
            case "podium-blue":
                center = new Vector2(.5f, 1f - 260f / 724f);
                break;
            case "formation-stage":
            case "team-formation-stage":
                center = new Vector2(.5f, 1f - 15f / 56f);
                break;
            case "stage-blue":
            case "deck-stage-blue":
            case "stage-purple":
            case "deck-stage-purple":
                center = new Vector2(.5f, 1f - 58f / 241f);
                break;
            case "stage-orange":
            case "deck-stage-orange":
                center = new Vector2(.5f, 1f - 57f / 239f);
                break;
            default:
                return false;
        }
        Rect drawn = stage.GetPixelAdjustedRect();
        Vector2 size = stage.sprite.rect.size;
        if (stage.preserveAspect && size.x > 0 && size.y > 0) {
            float aspect = size.x / size.y;
            Vector2 pivot = stage.rectTransform.pivot;
            if (aspect > drawn.width / drawn.height) {
                float old = drawn.height;
                drawn.height = drawn.width / aspect;
                drawn.y += (old - drawn.height) * pivot.y;
            } else {
                float old = drawn.width;
                drawn.width = drawn.height * aspect;
                drawn.x += (old - drawn.width) * pivot.x;
            }
        }
        world = stage.rectTransform.TransformPoint(new Vector3(drawn.x + drawn.width * center.x, drawn.y + drawn.height * center.y, 0));
        return true;
    }

}
