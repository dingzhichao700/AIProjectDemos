Shader "Starfront/BattleGround" {
    Properties {
        _MainTex ("Ground", 2D) = "white" {}
        _SceneOwners ("Baked pixel ownership", 2D) = "black" {}
        _CellMap ("Runtime camps", 2D) = "black" {}
    }
    SubShader {
        Cull Off ZWrite Off ZTest Always Blend Off
        Pass {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex, _SceneOwners, _CellMap;
            float4 _RedTint, _BlueTint;
            float _Strength, _Brightness, _Contrast;
            float4 frag(v2f_img input) : SV_Target {
                float4 source = tex2D(_MainTex, input.uv);
                float4 owner = tex2Dlod(_SceneOwners, float4(input.uv, 0, 0));
                // Permanent-neutral artwork bypasses ALL operations, including luminance adjustment.
                if (owner.a < .5 || round(owner.b * 255) == 3) { return source; }
                float2 address = round(owner.rg * 255);
                float camp = round(tex2Dlod(_CellMap, float4((address + .5) / 128, 0, 0)).r * 255);
                if (camp != 1 && camp != 2) { return source; }
                float3 encoded = source.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    encoded = LinearToGammaSpace(encoded);
                #endif
                float gray = dot(encoded, float3(.2126, .7152, .0722));
                gray = saturate(((gray - .5) * _Contrast + .5) * _Brightness);
                float3 tint = camp == 1 ? _BlueTint.rgb : _RedTint.rgb;
                float3 overlay = gray < .5 ? 2 * gray * tint : 1 - 2 * (1 - gray) * (1 - tint);
                float3 result = lerp(gray.xxx, overlay, _Strength);
                #ifndef UNITY_COLORSPACE_GAMMA
                    result = GammaToLinearSpace(result);
                #endif
                return float4(result, source.a);
            }
            ENDCG
        }
    }
}
