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

    /**用于丢弃对象销毁、回收或重新播放后返回的旧加载结果*/
    private int loadVersion;
    /**是否需要循环播放*/
    private bool loop;
    /**使用的计时器类型*/
    private TimerType timerType;
    /**播放完成回调*/
    private Handler playOverHandler;
    /**播放完成后是否自动回收*/
    private bool recoverAfterPlay;
    /**是否需要停在第一帧（某些角色动画用到，比如停在出生动作的首帧）*/
    private bool needStopAtFirstFrame;
    /**当前实例是否已进入对象池*/
    internal bool isInPool { get; private set; }
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
    /// <param name="handler">播放完成回调</param>
    /// <param name="recoverAfterPlay">播放完成后是否自动回收</param>
    /// <param name="scale">缩放倍率</param>
    /// <param name="dir">方向（-1左1右）</param>
    /// <param name="playSpeed">播放速度</param>
    /// <param name="timerType">使用的计时器类型</param>
    public async void Play(string path, bool loop = true, Handler handler = null, bool recoverAfterPlay = true, float scale = 1f, int dir = 1, float playSpeed = 1f, TimerType timerType = TimerType.COMMON) {
        ResetPlayback(false);
        _path = path;
        int requestVersion = loadVersion;
        this.loop = loop;
        if (loop) {
            if (handler != null) {
                Debug.LogWarning("警告：帧动画循环播放时不应设置回调");
                ReturnHandler(handler);
            }
        } else {
            playOverHandler = handler;
        }
        this.recoverAfterPlay = recoverAfterPlay;
        this.timerType = timerType;
        this.dir = dir;
        this.scale = scale;
        this.playSpeed = playSpeed;
        needStopAtFirstFrame = false;

        isPause = false;
        playedDuration = 0;
        playBeginTime = lastSyncTime = timer.curTime;
        timer.Loop(this, 20, OnLoop);
        OnLoop();
        UpdateScaleAndDirection();

        await LoadAndPlay(path, requestVersion);
    }

    /**加载并播放（如果未加载完成则会等待）*/
    private async Task LoadAndPlay(string path, int requestVersion) {
        FrameAnimationRes loadedData = FrameAnimationManager.GetRes(path);
        if (loadedData != null) {
            if (this != null && !isInPool && requestVersion == loadVersion && _path == path) {
                OnLoadComplete(loadedData);
            }
            return;
        }
        await ResourceLoader.LoadListAsync(new List<ResLoadInfo> { new ResLoadInfo(path, ResType.FrameAnim) }, () => {
            if (this == null || isInPool || requestVersion != loadVersion || _path != path) {
                return;
            }
            OnLoadComplete(FrameAnimationManager.GetRes(path));
        });
    }

    /**停在第一帧*/
    public async void StopAtFirstFrame(string path) {
        ResetPlayback(false);
        isPause = true;
        needStopAtFirstFrame = true;
        _path = path;
        playBeginTime = timer.curTime;
        int requestVersion = loadVersion;
        await LoadAndPlay(path, requestVersion);
    }

    /**加载帧动画完成*/
    public void OnLoadComplete(FrameAnimationRes data) {
        if (this == null || isInPool || data == null) {
            return;
        }
        float loadCostTime = timer.curTime - playBeginTime;
        if (loadCostTime > 300) {
            Debug.LogWarning("帧动画加载完成耗时：" + loadCostTime + "ms，资源：" + _path);
        }
        _animationData = data;
        trans.pivot = animationData.pivot;
        if (isPause || needStopAtFirstFrame) {
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
            playedDuration += passTime * playSpeed;
            if (animationData != null) {
                if (playedDuration > animationData.totalDuration) {
                    playedDuration = loop ? 0 : animationData.totalDuration;
                    if (!loop) {
                        CompletePlayback();
                        return;
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

    /**完成单次播放并按当前播放约定释放实例*/
    private void CompletePlayback() {
        int completedVersion = loadVersion;
        Handler handler = playOverHandler;
        playOverHandler = null;
        try {
            dispacher.Dispatch(FrameAnimationEvent.PLAY_COMPLETE);
            handler?.Run();
        } finally {
            ReturnHandler(handler);
            if (recoverAfterPlay && completedVersion == loadVersion && !isInPool) {
                Recover();
            }
        }
    }

    /**强制播放某时刻的对应的帧*/
    public void ForcePlayByTime(int duration) {
        if (animationData == null) {
            return;
        }
        Sprite frameSprite = animationData.GetSpriteByTime(duration);
        if (frameSprite != null) {
            playedDuration = duration;
            image.rectTransform.sizeDelta = new Vector2(frameSprite.texture.width, frameSprite.texture.height);
            SetSprite(frameSprite);
        }
    }

    /**设置图片的精灵（精灵为空则图片设为不可见）*/
    private void SetSprite(Sprite sp) {
        image.sprite = sp;
        image.gameObject.SetActive(sp != null);
    }

    /**暂停播放*/
    public void Pause() {
        isPause = true;
    }

    /**继续播放*/
    public void Continue() {
        isPause = false;
        lastSyncTime = timer.curTime;
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

    /**清理播放状态与外部监听*/
    public void Clear() {
        ResetPlayback(true);
    }

    /**归还对象池*/
    public void Recover() {
        if (isInPool) {
            return;
        }
        Clear();
        FrameAnimationManager.RecoverItem(this);
    }

    /**彻底销毁实例*/
    public void Destroy() {
        FrameAnimationManager.RemoveItem(this);
        isInPool = false;
        Clear();
        GameObject.Destroy(gameObject);
    }

    /**标记实例已进入对象池*/
    internal void MarkRecovered() {
        isInPool = true;
    }

    /**标记实例已离开对象池*/
    internal void MarkRented() {
        isInPool = false;
    }

    /**重置当前播放，保留监听时可用于同一实例切换动画*/
    private void ResetPlayback(bool clearDispatcher) {
        loadVersion++;
        timer.Clear(this, OnLoop);
        Handler handler = playOverHandler;
        playOverHandler = null;
        ReturnHandler(handler);
        _path = null;
        loop = false;
        recoverAfterPlay = false;
        needStopAtFirstFrame = false;
        _animationData = null;
        _playedDuration = 0f;
        playBeginTime = 0f;
        lastSyncTime = 0f;
        isPause = false;
        _scale = 1f;
        _dir = 1;
        _playSpeed = 1f;
        SetSprite(null);
        if (clearDispatcher) {
            dispacher.Clear();
        }
        timerType = TimerType.COMMON;
    }

    /**释放播放完成回调持有的引用*/
    private static void ReturnHandler(Handler handler) {
        if (handler == null) {
            return;
        }
        handler.caller = null;
        handler.callback = null;
        Handler.ReturnToPool(handler);
    }

    /**获取一个实例*/
    public static FrameAnimationView GetInstance() {
        return FrameAnimationManager.GetInstance();
    }

}
