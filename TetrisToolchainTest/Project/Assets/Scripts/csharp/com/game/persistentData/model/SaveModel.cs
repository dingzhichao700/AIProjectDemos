using cfg;

/// <summary>
/// Title baseline：仅用户设置（不含存档槽）。
/// </summary>
public class SaveModel
{
    SaveUserSettingVO userSettingVO;
    SaveGameDataVO gameDataVO;

    public void InitByUserSetting()
    {
        userSettingVO = JsonFileUtil.Load<SaveUserSettingVO>(PersistentDataConst.USER_SETTING);
        userSettingVO.SettingCorret();
        SealUserSetting();
    }

    public void InitByGameData()
    {
        gameDataVO = JsonFileUtil.Load<SaveGameDataVO>(PersistentDataConst.LOCAL_DATA);
        SealGameData();
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

    public int GetTetrisHighScore()
    {
        return gameDataVO != null ? gameDataVO.tetrisHighScore : 0;
    }

    public void SetTetrisHighScore(int value)
    {
        if (gameDataVO == null)
        {
            gameDataVO = new SaveGameDataVO();
        }

        if (value > gameDataVO.tetrisHighScore)
        {
            gameDataVO.tetrisHighScore = value;
            SealGameData();
        }
    }

    private void SealGameData()
    {
        JsonFileUtil.Save(PersistentDataConst.LOCAL_DATA, gameDataVO);
    }
}
