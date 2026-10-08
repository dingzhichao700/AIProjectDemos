/// <summary>
/// MonoBehaviour 对象池委托。baseline 仅保留帧动画视图池。
/// </summary>
public class ObjectPoolDelegate
{
    readonly MonoBehaviourObjectPool<FrameAnimationView> frameAnimeViewPool = new MonoBehaviourObjectPool<FrameAnimationView>();

    public FrameAnimationView GetAnimeView()
    {
        return frameAnimeViewPool.GetObject();
    }

    public void RecoverAnimeView(FrameAnimationView vo)
    {
        frameAnimeViewPool.ReturnObject(vo);
    }
}
