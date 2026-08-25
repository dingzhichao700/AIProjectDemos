using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 帧动画组件
/// 使用原则：动画播放并不会等待资源加载完成！因此需要预加载使用的帧动画资源，再创建对应的帧动画组件去加载相关资源。
/// </summary>
public class FrameAnimationView : MonoBehaviour {

    private Image image;

    /**是否暂停*/
    public bool isPause;

    /**当前正在加载或播放的帧动画路径*/
    private string _path;
    public string path => _path;
    /**是否需要循环播放*/
    private bool loop;
    /**使用的计时器类型*/
    private TimerType timerType;
    /**播放完成回调*/
    private Handler playOverHandler;
    /**播放完成是否销毁*/
    private bool playOverDestory;
    /**是否需要停在第一帧（某些角色动画用到，比如停在出生动作的首帧）*/
    private bool needStopAtFirstFrame;
    /**缩放*/
    private float _scale;
    /**方向（-1左1右）*/
    private int _dir;

    /**播放速度*/
    private float _playSpeed;
    public float playSpeed {
        get {
            return _playSpeed;
        }
        set {
            _playSpeed = value;
        }
    }

    /**使用的计时器*/
    private Timer timer => RookieEngine.GetTimer(timerType);

    /**当前播放开始时刻*/
    private float playBeginTime;

    /**所使用的动画数据*/
    private FrameAnimationRes _animationData;
    public FrameAnimationRes animationData => _animationData;

    /**当前已播放时长（循环播放的话会不断从0重新开始）*/
    private float _playedDuration;
    public float playedDuration {
        get {
            return _playedDuration;
        }
        set {
            _playedDuration = value;
        }
    }

    /**上次同步时间*/
    private float lastSyncTime;

    public EventDispatcher dispacher;

    public RectTransform trans => transform as RectTransform;

    void Awake() {
        image = gameObject.AddComponent<Image>() as Image;
        image.color = Color.white;
        image.raycastTarget = false;
        image.gameObject.SetActive(false);
        image.material = ResourceManager.GetMaterial(ResourceConst.PATH_MATERIAL + "custom/matInstanceImage");
        dispacher = new EventDispatcher();
    }

    /// <summary>
    /// 播放
    /// </summary>
    /// <param name="path">帧动画路径</param>
    /// <param name="loop">是否循环播放</param>
    /// <param name="playOverDestory">播放完成后自动销毁</param>
    /// <param name="handler">播放完成回调</param>
    /// <param name="scale">缩放倍率</param>
    /// <param name="dir">方向（-1左1右）</param>
    /// <param name="playSpeed">播放速度</param>
    /// <param name="timerType">使用的计时器类型</param>
    public async void Play(string path, bool loop = true, Handler handler = null, bool playOverDestory = true, float scale = 1f, int dir = 1, float playSpeed = 1f, TimerType timerType = TimerType.COMMON) {
        this._path = path;
        this.loop = loop;
        if (loop) {
            //循环
            if (handler != null) {
                Debug.LogWarning("警告：帧动画循环播放时不应设置回调");
            }
        } else {
            //不循环
            this.playOverHandler = handler;
        }
        this.playOverDestory = playOverDestory;
        this.timerType = timerType;
        this.dir = dir;
        this.scale = scale;
        this.playSpeed = playSpeed;
        needStopAtFirstFrame = false;

        //设置播放相关数据
        isPause = false;
        playSpeed = 1;
        playedDuration = 0;
        playBeginTime = lastSyncTime = timer.curTime;
        _animationData = null;
        timer.Loop(this, 20, OnLoop);
        OnLoop();
        UpdateScaleAndDirection();

        //加载资源
        await LoadAndPlay(path);
    }

    /**加载并播放（如果未加载完成则会等待）*/
    private async Task LoadAndPlay(string path) {
        await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(path, ResType.FrameAnim) }, () => {
            OnLoadComplete(FrameAnimationManager.GetRes(path));
        });
    }

    /**停在第一帧*/
    public async void StopAtFirstFrame(string path) {
        Pause();
        //加载资源
        await LoadAndPlay(path);
    }

    /**加载帧动画完成*/
    public void OnLoadComplete(FrameAnimationRes data) {
        float loadCostTime = timer.curTime - playBeginTime;
        if (loadCostTime > 300) {
            Debug.LogWarning("帧动画加载完成耗时：" + loadCostTime + "ms，资源：" + _path);
        } else {
            //Debug.Log("帧动画加载完成耗时：" + loadCostTime + "ms，资源："+ path);
        }
        _animationData = data;
        if (animationData != null) {
            trans.pivot = animationData.pivot;
        }
        if (isPause) {
            Sprite firstFrameSprite = animationData.GetSpriteByTime(0);
            if (firstFrameSprite != null) {
                image.rectTransform.sizeDelta = new Vector2(firstFrameSprite.texture.width, firstFrameSprite.texture.height);
                SetSprite(firstFrameSprite);
            }
        }
    }

    private void OnLoop() {
        float passTime = timer.curTime - lastSyncTime;
        if (!isPause) {
            //先累加播放时长
            playedDuration += passTime * playSpeed;
            if (animationData != null) {
                //动画数据已生成
                if (playedDuration > animationData.totalDuration) {//时长超出动画时长
                    //播放完成回调执行
                    playedDuration = loop ? 0 : animationData.totalDuration; //循环播放的话，把播放时刻戳重置为0
                    Handler handler = playOverHandler;
                    if (playOverHandler != null) {
                        playOverHandler = null;
                    }
                    if (!loop) {//不循环播放的话
                        dispacher.Dispatch(FrameAnimationEvent.PLAY_COMPLETE);
                        if (playOverDestory) {
                            Destroy(); //需要自动销毁的话，到这一步就可以销毁了
                        }
                    }
                    if (handler != null) {
                        handler.Run();
                    }
                } else {
                    Sprite frameSprite = animationData.GetSpriteByTime(playedDuration);
                    if (frameSprite != null) {
                        image.rectTransform.sizeDelta = new Vector2(frameSprite.texture.width, frameSprite.texture.height);
                        SetSprite(frameSprite);
                    }
                }
            }
            dispacher.Dispatch(FrameAnimationEvent.PLAY_PROCESS_UPDATE);
        }
        lastSyncTime = timer.curTime;
    }

    /**强制播放某时刻的对应的帧*/
    public void ForcePlayByTime(int duration) {
        Sprite frameSprite = animationData.GetSpriteByTime(duration);
        if (frameSprite != null) {
            playedDuration = duration;
            image.rectTransform.sizeDelta = new Vector2(frameSprite.texture.width, frameSprite.texture.height);
            SetSprite(frameSprite);
        }
    }

    /**设置图片的精灵（精灵为空则图片设为不可见）*/
    private void SetSprite(Sprite sp) {
        image.gameObject.SetActive(sp != null);
        if (sp != null) {
            image.sprite = sp;
        }
    }

    /**暂停播放*/
    public void Pause() {
        isPause = true;
    }

    /**继续播放*/
    public void Continue() {
        isPause = false;
    }

    /**停止播放*/
    public void Stop() {
        isPause = true;
        SetSprite(null);
    }

    public float scale {
        set {
            if (_scale != value) {
                _scale = value;
                UpdateScaleAndDirection();
            }
        }
        get {
            return _scale;
        }
    }

    public int dir {
        set {
            if (_dir != value) {
                _dir = value;
                UpdateScaleAndDirection();
            }
        }
        get {
            return _dir;
        }
    }

    /**更新缩放和朝向*/
    private void UpdateScaleAndDirection() {
        transform.localScale = new Vector3(scale * dir, scale, scale);
    }

    /**清理*/
    public void Clear() {
        _scale = 0;
        _dir = 0;
        isPause = default;
        SetSprite(null);
        timer.Clear(this, OnLoop);
    }

    /**销毁*/
    public void Destroy(bool needRecover = false) {
        Clear();
        if (needRecover) {
            FrameAnimationManager.RecoverItem(this);
        } else {
            GameObject.Destroy(this.gameObject);
        }
    }

    /**获取一个实例*/
    public static FrameAnimationView GetInstance() {
        return FrameAnimationManager.GetInstance();
        //return RookieEngine.monoPool.GetAnimeView();//使用这个接口会有问题，MapUnitView的AppearanceView中的FrameAnimationView坐标会发生偏移，原因暂未找到
    }

}
