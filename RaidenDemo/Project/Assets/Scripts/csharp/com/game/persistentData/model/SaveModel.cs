using cfg;

/// <summary>
/// Title baseline：仅用户设置（不含存档槽）。
/// </summary>
public class SaveModel
{
    SaveUserSettingVO userSettingVO;

    public void InitByUserSetting()
    {
        userSettingVO = JsonFileUtil.Load<SaveUserSettingVO>(PersistentDataConst.USER_SETTING);
        userSettingVO.SettingCorret();
        SealUserSetting();
    }

    public void SealUserSetting()
    {
        JsonFileUtil.Save(PersistentDataConst.USER_SETTING, userSettingVO);
    }

    public string GetSetting(SettingOptionSelection selection)
    {
        return userSettingVO.GetSetting(selection);
    }

    public void SetOptionSelectValue(SettingOptionSelection selection, string value)
    {
        if (userSettingVO.SetOptionSelectValue(selection, value))
        {
            SealUserSetting();
        }
    }
}
