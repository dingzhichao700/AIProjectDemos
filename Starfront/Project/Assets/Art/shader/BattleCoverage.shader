Shader "Starfront/BattleCoverage" {
 Properties {
  [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
  _Blue ("Friendly", 2D) = "white" {}
  _Orange ("Enemy", 2D) = "white" {}
  _CellStates ("Cell states", 2D) = "black" {}
  _SceneSize ("Scene size", Vector) = (2000,2000,0,0)
  _Origin ("Origin", Vector) = (1000,1000,0,0)
  _Thickness ("Ground thickness", Float) = 10
  _StencilComp ("Stencil Comparison", Float) = 8
  _Stencil ("Stencil ID", Float) = 0
  _StencilOp ("Stencil Operation", Float) = 0
  _StencilWriteMask ("Stencil Write Mask", Float) = 255
  _StencilReadMask ("Stencil Read Mask", Float) = 255
  _ColorMask ("Color Mask", Float) = 15
 }
 SubShader {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
  Blend SrcAlpha OneMinusSrcAlpha
  ColorMask [_ColorMask]
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
   struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 local:TEXCOORD1; };
   sampler2D _Blue, _Orange, _CellStates;
   float4 _SceneSize, _Origin, _ClipRect;
   float _Thickness;
   v2f vert(appdata v) { v2f o; o.local=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=v.uv; return o; }
   fixed4 frag(v2f i):SV_Target {
    float2 p=float2(i.uv.x*_SceneSize.x,(1-i.uv.y)*_SceneSize.y);
    int q0=(int)round((p.x-_Origin.x)/80.25);
    int k0=(int)round((p.y+_Thickness-_Origin.y)/46.5);
    float best=-100000; float2 selected=0; float2 center=0;
    // One geometric owner per pixel. Adjacent fills and bright edges never accumulate.
    for(int dq=-1;dq<=1;dq++) {
     for(int dk=-2;dk<=2;dk++) {
      int q=q0+dq,k=k0+dk;
      if((q-k)%2!=0) continue;
      float2 c=_Origin.xy+float2(80.25*q,46.5*k-_Thickness);
      float2 delta=abs(p-c);
      float d=min(46.5-delta.y, (53.5-delta.x-(53.5/93.0)*delta.y)/1.153664);
      if(d>=-0.0001 && d>best) { best=d;selected=float2(q,k);center=c; }
     }
    }
    if(best < -0.0001) return 0;
    float2 address=(selected+64.5)/128.0;
    fixed4 state=tex2D(_CellStates,address);
    int camp=(int)round(state.r*255);
    if(state.a<.5 || (camp!=1 && camp!=2)) return 0;
    float2 uv=(p-center+float2(56,49))/float2(112,98);
    uv.y=1-uv.y;
    fixed4 color=camp==1?tex2D(_Blue,uv):tex2D(_Orange,uv);
    color*=i.color;
    #ifdef UNITY_UI_CLIP_RECT
    color.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
    #endif
    return color;
   }
   ENDCG
  }
 }
}

