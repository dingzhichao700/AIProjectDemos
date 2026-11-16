using UnityEngine;

/// <summary>R8批准立绘的生产画布映射；只保存表现几何，不承载业务状态。</summary>
public static class NativePortraitGeometry {
 public readonly struct Entry {
  public readonly Vector2 canvas, pivot, visible, sourceCanvas, offset; public readonly float scale;
  public Entry(Vector2 c,Vector2 p,Vector2 b,Vector2 s,Vector2 o,float k){canvas=c;pivot=p;visible=b;sourceCanvas=s;offset=o;scale=k;}
 }
 public static bool TryGet(int id,Sprite sprite,out Entry e){
  e=default; switch(id){
case 2101: e=new Entry(new Vector2(283.000000000f, 334.000000000f),new Vector2(0.447611198f, 0.154749522f),new Vector2(189.000000000f, 252.000000000f),new Vector2(157.000000000f, 194.000000000f),new Vector2(32.000000000f, 32.000000000f),1.391752577f);break;
case 2102: e=new Entry(new Vector2(318.000000000f, 250.000000000f),new Vector2(0.522400435f, 0.129582440f),new Vector2(212.000000000f, 147.000000000f),new Vector2(1536.000000000f, 1024.000000000f),new Vector2(32.000000000f, 32.000000000f),0.165054563f);break;
case 2103: e=new Entry(new Vector2(340.000000000f, 321.000000000f),new Vector2(0.553457017f, 0.225009046f),new Vector2(258.000000000f, 222.000000000f),new Vector2(1299.000000000f, 1211.000000000f),new Vector2(32.000000000f, 32.000000000f),0.211832170f);break;
case 2104: e=new Entry(new Vector2(306.000000000f, 334.000000000f),new Vector2(0.470431613f, 0.162573714f),new Vector2(142.000000000f, 255.000000000f),new Vector2(1188.000000000f, 1324.000000000f),new Vector2(32.000000000f, 32.000000000f),0.203299720f);break;
case 2105: e=new Entry(new Vector2(400.000000000f, 298.000000000f),new Vector2(0.539676871f, 0.108516564f),new Vector2(303.000000000f, 203.000000000f),new Vector2(1536.000000000f, 1024.000000000f),new Vector2(32.000000000f, 32.000000000f),0.218555507f);break;
case 2201: e=new Entry(new Vector2(284.000000000f, 259.000000000f),new Vector2(0.474608764f, 0.211809769f),new Vector2(212.000000000f, 173.000000000f),new Vector2(252.000000000f, 223.000000000f),new Vector2(32.000000000f, 32.000000000f),0.873015873f);break;
case 2202: e=new Entry(new Vector2(284.000000000f, 304.000000000f),new Vector2(0.549993130f, 0.169852375f),new Vector2(203.000000000f, 227.000000000f),new Vector2(205.000000000f, 223.000000000f),new Vector2(32.000000000f, 32.000000000f),1.073170732f);break;
case 2203: e=new Entry(new Vector2(324.000000000f, 349.000000000f),new Vector2(0.537087872f, 0.092273723f),new Vector2(244.000000000f, 263.000000000f),new Vector2(238.000000000f, 256.000000000f),new Vector2(32.000000000f, 32.000000000f),1.090756303f);break;
case 2204: e=new Entry(new Vector2(241.000000000f, 214.000000000f),new Vector2(0.565407847f, 0.286738436f),new Vector2(169.000000000f, 131.000000000f),new Vector2(158.000000000f, 134.000000000f),new Vector2(32.000000000f, 32.000000000f),1.113924051f);break;
case 2205: e=new Entry(new Vector2(380.000000000f, 301.000000000f),new Vector2(0.556853549f, 0.254057911f),new Vector2(294.000000000f, 211.000000000f),new Vector2(1448.000000000f, 1086.000000000f),new Vector2(32.000000000f, 32.000000000f),0.218093487f);break;
case 2301: e=new Entry(new Vector2(324.000000000f, 303.000000000f),new Vector2(0.513861629f, 0.214523210f),new Vector2(232.000000000f, 230.000000000f),new Vector2(1308.000000000f, 1202.000000000f),new Vector2(32.000000000f, 32.000000000f),0.198728010f);break;
case 2302: e=new Entry(new Vector2(323.000000000f, 291.000000000f),new Vector2(0.512063225f, 0.203854726f),new Vector2(241.000000000f, 211.000000000f),new Vector2(1341.000000000f, 1173.000000000f),new Vector2(32.000000000f, 32.000000000f),0.192758253f);break;
case 2303: e=new Entry(new Vector2(341.000000000f, 281.000000000f),new Vector2(0.500226045f, 0.123445737f),new Vector2(226.000000000f, 176.000000000f),new Vector2(1418.000000000f, 1109.000000000f),new Vector2(32.000000000f, 32.000000000f),0.195041635f);break;
case 2304: e=new Entry(new Vector2(328.000000000f, 286.000000000f),new Vector2(0.511700079f, 0.226844112f),new Vector2(228.000000000f, 206.000000000f),new Vector2(1367.000000000f, 1150.000000000f),new Vector2(32.000000000f, 32.000000000f),0.192677484f);break;
case 2305: e=new Entry(new Vector2(399.000000000f, 301.000000000f),new Vector2(0.508341561f, 0.106535790f),new Vector2(264.000000000f, 202.000000000f),new Vector2(1536.000000000f, 1024.000000000f),new Vector2(32.000000000f, 32.000000000f),0.217893218f);break;
case 2306: e=new Entry(new Vector2(319.000000000f, 300.000000000f),new Vector2(0.494018988f, 0.106717818f),new Vector2(195.000000000f, 218.000000000f),new Vector2(1310.000000000f, 1201.000000000f),new Vector2(32.000000000f, 32.000000000f),0.194565542f);break;
default:return false; }
 return sprite!=null && sprite.name=="portrait" && sprite.rect.size==e.canvas && (sprite.pivot/e.canvas-e.pivot).sqrMagnitude<.000001f;
 }
 public static Vector3 MapSkinPixel(int id,Sprite original,Sprite skin,Vector2 pixel,Rect drawn){
  Vector2 uv=pixel/skin.rect.size;
  if(TryGet(id,original,out var e)){
   Vector2 size=e.sourceCanvas*e.scale;
   Vector2 native=new Vector2(uv.x*size.x+e.offset.x,uv.y*size.y+e.canvas.y-e.offset.y-size.y);
   uv=native/e.canvas;
  }
  return new Vector3(drawn.x+uv.x*drawn.width,drawn.y+uv.y*drawn.height,0);
 }
}
