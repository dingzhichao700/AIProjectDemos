#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class CardCultivationRuntimeValidation {
    private static async Task RunGovernanceB1DetailImages(){
        Check(typeof(CardSkillDetailPanel).GetMethod("ResolveIconAddress",BindingFlags.Static|BindingFlags.NonPublic)==null,"R3详情直接读取配置，无类型换图");
        var a=new GameObject("R3 owned image lease A",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(CultivationImageLease));
        var b=new GameObject("R3 owned image lease B",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(CultivationImageLease));
        var first=a.GetComponent<CultivationImageLease>();var second=b.GetComponent<CultivationImageLease>();
        try{
            foreach(var skill in CfgManager.tables.CultivationSkillObj.DataList){
                string expected="Assets/Art/unpack/default/skillIcons/"+skill.Id+".png";
                Check(skill.Icon==expected,"R3技能配置散图且ID平铺="+skill.Id);
                await ValidateFlatIcon(expected,skill.Id.ToString(),a,b,first,second);
            }
            var map=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ResourceData/default/item/icon-map.json");Check(map!=null,"R3道具映射按数据类型归档且原GUID可用");
            foreach(var item in CfgManager.tables.ItemObj.DataList){
                string expected="Assets/Art/unpack/default/itemIcons/"+item.Id+".png";
                Check(item.IconId==item.Id&&CultivationItemIconResources.TryGetAddress(map,item.Id,out string address)&&address==expected,"R3道具配置/映射/ID一致="+item.Id);
                await ValidateFlatIcon(expected,item.Id.ToString(),a,b,first,second);
            }
        }finally{first.Clear();second.Clear();UnityEngine.Object.Destroy(a);UnityEngine.Object.Destroy(b);}
    }
    private static async Task ValidateFlatIcon(string address,string id,GameObject a,GameObject b,CultivationImageLease first,CultivationImageLease second){
        var load=Task.WhenAll(first.BindAsync(address),second.BindAsync(address));
        if(await Task.WhenAny(load,Task.Delay(20000))!=load)throw new TimeoutException("R3 icon load "+address);await load;
        var sprite=a.GetComponent<Image>().sprite;
        Check(sprite!=null&&sprite==b.GetComponent<Image>().sprite,"R3共享同一散图Sprite="+address);
        Check(sprite.name==id&&sprite.rect.width>0&&sprite.rect.height>0,"R3文件ID与几何可用="+address);
        first.Clear();Check(b.GetComponent<Image>().sprite==sprite&&ResourceManager.GetUnpackImage(address)==sprite,"R3关闭一端不释放另一端="+address);
        second.Clear();await first.BindAsync(address);Check(a.GetComponent<Image>().sprite!=null,"R3全关闭可重开="+address);first.Clear();
    }
}
#endif
