using UnityEngine;

/// <summary>
/// 帧动画资源对象
/// </summary>
public class FrameAnimationRes {

    /**帧动画精灵图列表*/
    public Sprite[] sprites;
    /**每帧图像对应时长的列表*/
    public int[] durations;
    /**锚点信息*/
    public Vector2 pivot;
    /**总时长*/
    public int totalDuration;

    /**获取某时刻应显示的图像*/
    public Sprite GetSpriteByTime(float time) {
        return GetSpriteByIndex(GetIndexByTime(time));
    }

    /**根据索引获取动画帧*/
    public Sprite GetSpriteByIndex(int index) {
        if (sprites != null && sprites.Length > index) {
            return sprites[index];
        }
        return null;
    }

    /**获取某时刻对应的动画索引*/
    public int GetIndexByTime(float time) {
        int temp = 0;
        for (int i = 0; i < durations.Length; i++) {
            temp += durations[i];
            if (time <= temp) {
                return i;
            }
        }
        return 0;
    }

}
