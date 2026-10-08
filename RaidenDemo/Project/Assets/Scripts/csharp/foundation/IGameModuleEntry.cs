using System.Threading.Tasks;

/// <summary>
/// 业务模块的初始化入口。
/// </summary>
public interface IGameModuleEntry {

    /// <summary>
    /// 在底座服务就绪后初始化模块。
    /// </summary>
    Task InitializeAsync();

}
