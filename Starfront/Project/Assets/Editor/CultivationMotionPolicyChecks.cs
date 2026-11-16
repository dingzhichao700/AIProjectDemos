using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class MarineIdleIntegrationReview {
    private static async Task CheckCultivationMotionPolicy(Image formation, Image portrait) {
        var field=formation.GetComponent<CardArtIdleSkin>();var card=portrait.GetComponent<CardArtIdleSkin>();
        var fieldMouth=field.Rig.GetComponent<HydraliskMouthRig>();var mouth=card.Rig.GetComponent<HydraliskMouthRig>();
        field.StopSpecial();card.StopSpecial();card.AutomaticSpecialEnabled=false;
        float oldField=field.cultivationIdleSeconds,oldCard=card.cultivationIdleSeconds;
        field.cultivationIdleSeconds=.35f;card.cultivationIdleSeconds=.35f;
        Check(!field.CanPlaySpecial && !await field.PlaySpecialAsync() && !fieldMouth.PlayRoar(),"阵容核心入口及直接咆哮入口均拒绝攻击表现");
        field.AutomaticSpecialEnabled=true;await Frames(45);
        Check(!fieldMouth.IsRoaring && !field.IsSpecialPlaying,"阵容经过自动等待仍只播放空闲");
        card.AutomaticSpecialEnabled=true;double deadline=EditorApplication.timeSinceStartup+7;
        for(int round=0;round<2;round++) {
            while(!mouth.IsRoaring && EditorApplication.timeSinceStartup<deadline){await Frames(1);}
            Check(mouth.IsRoaring,"培养实际自动进入咆哮周期="+round);
            while(mouth.IsRoaring && EditorApplication.timeSinceStartup<deadline){await Frames(1);}
            await Frames(1);
            Check(!mouth.IsRoaring && mouth.OpenAmount==0,"培养实际自动返回空闲周期="+round);
        }
        card.AutomaticSpecialEnabled=false;card.StopSpecial();await Frames(2);
        float minimum=100,maximum=-100;float start=RookieEngine.timer.curTime;deadline=EditorApplication.timeSinceStartup+6;
        while(RookieEngine.timer.curTime-start<mouth.idleJawPeriodSeconds*1100 && EditorApplication.timeSinceStartup<deadline) {
            float angle=Mathf.DeltaAngle(mouth.idleJawDegrees,mouth.mandible.localEulerAngles.z);
            minimum=Mathf.Min(minimum,angle);maximum=Mathf.Max(maximum,angle);await Frames(1);
        }
        Check(maximum-minimum>4 && maximum-minimum<=mouth.idleJawAmplitudeDegrees*2+.1f,"空闲下颌持续小幅开合完整周期");
        File.WriteAllText(Path.Combine(SessionState.GetString(Key+".out",""),"jaw-idle-cycle.json"),"{\"minimumDegrees\":"+minimum+",\"maximumDegrees\":"+maximum+",\"timerSpanMs\":"+(RookieEngine.timer.curTime-start)+"}");
        field.cultivationIdleSeconds=oldField;card.cultivationIdleSeconds=oldCard;
        Check(!fieldMouth.IsRoaring,"培养两轮攻击不影响阵容空闲实例");
    }
}