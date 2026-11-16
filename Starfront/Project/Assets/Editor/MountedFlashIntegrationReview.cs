using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public static partial class MarineIdleIntegrationReview {
    private static async Task CheckMountedFlash(Image image) {
        var owner = image.GetComponent<CardArtIdleSkin>();
        owner.AutomaticSpecialEnabled = false; owner.StopSpecial(); await Frames(2);
        Check(await owner.PlayBurstAsync(), "帧火光与原动作共同预加载成功");
        await Frames(5);
        var burst = owner.Burst; var rig = burst.Rig; var flash = burst.MuzzleFlash;
        rig.paused = true; owner.Rig.paused = true;
        Check(flash != null && flash.Clip.sprites.Length == 6 && flash.Clip.totalDuration == 120, "已批准六帧120ms火光就绪");
        Check(flash.Clip.pivot == new Vector2(.25f,.5f) && flash.Clip.sourceSizes[0] == new Vector2(512,512), "原画布发射锚点与裁剪偏移保留");
        Check(burst.mainTexture != flash.mainTexture, "火光独立纹理不再使用程序多边形");
        var elapsed = typeof(MarineBurstRig).GetProperty(nameof(MarineBurstRig.Elapsed));
        for (int shot=0;shot<3;shot++) {
            for (int frame=0;frame<6;frame++) {
                float t=rig.shotStartSeconds+shot*rig.shotInterval+(frame*.02f+.01f);
                elapsed.SetValue(rig,t);rig.ApplyPose();await Frames(2);Canvas.ForceUpdateCanvases();
                Check(flash.FrameIndex==frame && burst.FlashShot==shot,"逐发同步实际帧="+shot+"/"+frame);
                Check(flash.canvasRenderer.GetMesh().vertexCount==4 && burst.canvasRenderer.GetMesh().vertexCount==1146,"火光贴图四边形与四层蒙皮独立绘制");
                Check(flash.Attachment.sqrMagnitude>0 && flash.PixelScale>0 && Mathf.Abs(flash.Forward.magnitude-1)<.0001f,"挂点位置及射向有效");
                if(frame==1) { Capture("mounted-flash-shot-"+shot+"-peak.png"); }
            }
            elapsed.SetValue(rig,rig.shotStartSeconds+shot*rig.shotInterval+.14f);rig.ApplyPose();await Frames(2);
            Check(flash.FrameIndex==-1 && flash.canvasRenderer.GetMesh().vertexCount==0,"每发120ms后火光消失="+shot);
        }
        elapsed.SetValue(rig,rig.shotStartSeconds+.03f);rig.ApplyPose();await Frames(2);
        int frozen=flash.FrameIndex;Vector2 point=flash.Attachment;await Frames(5);
        Check(flash.FrameIndex==frozen && flash.Attachment==point,"暂停不推进帧或挂点");
        var group=image.gameObject.AddComponent<CanvasGroup>();group.alpha=.35f;await Frames(2);
        Check(Math.Abs(flash.canvasRenderer.GetInheritedAlpha()-.35f)<.02f,"挂点火光继承CanvasGroup透明");
        group.alpha=1;UnityEngine.Object.Destroy(group);
        rig.paused=false;owner.Rig.paused=false;owner.StopSpecial();await Frames(4);
        Check(UnityEngine.Object.FindObjectsOfType<MountedFrameGraphic>().Length==0 && MarineBurstGraphic.ActiveFlashLeases==0 && !ResourceManager.HasLoadedUnpackImage(MarineBurstGraphic.FlashAddress+".png"),"取消后帧图形和专用图集租约归零");
    }
}
