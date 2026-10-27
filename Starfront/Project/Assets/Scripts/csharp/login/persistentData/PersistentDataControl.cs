/// <summary>
/// Title baseline：用户设置读写。
/// </summary>
public class PersistentDataControl : EventDispatcher
{
    SaveModel _saveModel;
    public SaveModel saveModel => _saveModel;

    static PersistentDataControl _ins;
    public static PersistentDataControl ins
    {
        get
        {
            if (_ins == null)
            {
                _ins = new PersistentDataControl();
            }

            return _ins;
        }
    }

    public PersistentDataControl()
    {
        _saveModel = new SaveModel();
    }

    public void ReadUserSetting()
    {
        saveModel.InitByUserSetting();
    }
}
