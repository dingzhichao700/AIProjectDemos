/// <summary>
/// 资源加载结构
/// </summary>
public struct ResLoadInfo {

    public string path;

    public ResType resType;

    public ResLoadInfo(string path, ResType resType) {
        this.path = path;
        this.resType = resType;
    }

}
