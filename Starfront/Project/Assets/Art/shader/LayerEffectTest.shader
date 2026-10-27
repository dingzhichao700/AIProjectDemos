Shader "Starfront/LayerEffectTest" {
    Properties {
        _MainTex ("Terrain", 2D) = "white" {}
        _CellMap ("Paint overrides", 2D) = "black" {}
        _HoleMask ("Local cuts", 2D) = "black" {}
    }
    SubShader {
        Cull Off ZWrite Off ZTest Always Blend Off
        Pass {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _CellMap;
            sampler2D _HoleMask;
            sampler2D _SceneOwners;
            float _GroundScene;
            float _SwapCamps;
            float4 _SourceUV;
            float2 _SourceGrayOffsets;
            float _GridLineWidth;
            float _GridLineOpacity;
            float4 _CanvasSize;
            float4 _Hex;
            float4 _RedTint;
            float4 _BlueTint;
            float _DisplayMode;
            float _Layout;
            float _Boundary;
            float _Strength;
            float _Brightness;
            float _Contrast;
            float _ShowGrid;

            float2 hexCell(float2 pixelPoint) {
                float2 estimate = round((pixelPoint - _Hex.xy) / float2(_Hex.z * .75, _Hex.w * .5));
                float2 best = estimate;
                float distance = 1e20;
                [unroll] for (int x = -1; x <= 1; x++) {
                    [unroll] for (int y = -2; y <= 2; y++) {
                        float2 cell = estimate + float2(x, y);
                        if (abs(frac((cell.x - cell.y) * .5)) < .1) {
                            float2 center = _Hex.xy + cell * float2(_Hex.z * .75, _Hex.w * .5);
                            float2 delta = (pixelPoint - center) / float2(_Hex.z * .5, _Hex.w / 1.732050808);
                            float d = dot(delta, delta);
                            if (d < distance) {
                                best = cell;
                                distance = d;
                            }
                        }
                    }
                }
                return best;
            }

            float4 cellData(float2 cell) {
                if (any(cell < -63) || any(cell > 63)) {
                    return 0;
                }
                return tex2Dlod(_CellMap, float4((cell + 64.5) / 128, 0, 0));
            }

            float holeAt(float2 samplePoint) {
                float2 uv = samplePoint / _CanvasSize.xy;
                uv.y = 1 - uv.y;
                return max(cellData(hexCell(samplePoint)).b, tex2Dlod(_HoleMask, float4(uv, 0, 0)).r);
            }

            float3 blendColor(float gray, float3 tint) {
                if (_DisplayMode < 2.5) {
                    return gray * tint;
                }
                if (_DisplayMode < 3.5) {
                    return gray < .5 ? 2 * gray * tint : 1 - 2 * (1 - gray) * (1 - tint);
                }
                if (_DisplayMode < 4.5) {
                    float d = gray <= .25 ? ((16 * gray - 12) * gray + 4) * gray : sqrt(gray);
                    float3 darken = gray - (1 - 2 * tint) * gray * (1 - gray);
                    float3 lighten = gray + (2 * tint - 1) * (d - gray);
                    return lerp(darken, lighten, step(.5, tint));
                }
                if (_DisplayMode < 5.5) {
                    return 1 - (1 - gray) * (1 - tint);
                }
                return saturate(gray + tint);
            }

            float4 frag(v2f_img input) : SV_Target {
                float4 source = tex2D(_MainTex, input.uv * _SourceUV.xy + _SourceUV.zw);
                float3 encoded = source.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    encoded = LinearToGammaSpace(encoded);
                #endif
                if (_GroundScene > .5) {
                    float4 owner = tex2Dlod(_SceneOwners, float4(input.uv * _SourceUV.xy + _SourceUV.zw, 0, 0));
                    float camp = round(owner.b * 255);
                    float3 sceneResult = encoded;
                    // Neutral and unowned pixels bypass every color operation.
                    if (_DisplayMode > .5 && owner.a > .5 && camp > .5 && camp < 2.5) {
                        float luminance = dot(encoded, float3(.2126, .7152, .0722));
                        luminance = saturate(((luminance - .5) * _Contrast + .5) * _Brightness);
                        sceneResult = luminance.xxx;
                        if (_DisplayMode > 1.5) {
                            float blue = camp < 1.5 ? 1 : 0;
                            float2 cell = round(owner.rg * 255) - 64;
                            float4 painted = cellData(cell);
                            if (painted.r > .5) { blue = 0; }
                            else if (painted.g > .5) { blue = 1; }
                            blue = lerp(blue, 1 - blue, _SwapCamps);
                            sceneResult = lerp(luminance.xxx, blendColor(luminance, lerp(_RedTint.rgb, _BlueTint.rgb, blue)), _Strength);
                        }
                    }
                    #ifndef UNITY_COLORSPACE_GAMMA
                        sceneResult = GammaToLinearSpace(sceneResult);
                    #endif
                    return float4(sceneResult, source.a);
                }
                float3 result = encoded;
                if (_DisplayMode > .5) {
                    float gray = dot(encoded, float3(.2126, .7152, .0722));
                    // Balance the original red/blue material medians without flattening its texture.
                    gray += encoded.r >= encoded.b ? _SourceGrayOffsets.x : _SourceGrayOffsets.y;
                    gray = saturate((gray - .5) * _Contrast + .5);
                    gray = saturate(gray * _Brightness);
                    result = gray.xxx;
                    if (_DisplayMode > 1.5) {
                        float2 pixelPoint = float2(input.uv.x, 1 - input.uv.y) * _CanvasSize.xy;
                        float2 cell = hexCell(pixelPoint);
                        float2 center = _Hex.xy + cell * float2(_Hex.z * .75, _Hex.w * .5);
                        float blue = step(_CanvasSize.y * _Boundary, center.y);
                        if (_Layout > .5 && _Layout < 1.5) {
                            blue = 0;
                        } else if (_Layout > 1.5 && _Layout < 2.5) {
                            blue = 1;
                        } else if (_Layout > 2.5) {
                            blue = step(.25, frac((cell.x + (cell.y - cell.x) * .5) * .5));
                        }
                        float4 painted = cellData(cell);
                        if (painted.r > .5) {
                            blue = 0;
                        } else if (painted.g > .5) {
                            blue = 1;
                        }
                        float3 tint = lerp(_RedTint.rgb, _BlueTint.rgb, blue);
                        result = lerp(gray.xxx, blendColor(gray, tint), _Strength);
                        float2 local = abs(pixelPoint - center);
                        float slope = _Hex.z / (2 * _Hex.w);
                        float edge = min(_Hex.w * .5 - local.y, (_Hex.z * .5 - local.x - local.y * slope) / sqrt(1 + slope * slope));
                        float aa = max(fwidth(edge), .35);
                        float gridCoverage = 1 - smoothstep(_GridLineWidth - aa, _GridLineWidth + aa, edge);
                        result *= 1 - gridCoverage * _GridLineOpacity * _ShowGrid;
                        // The final coverage clips both surface and grid; union holes have no internal lines.
                        float hole = holeAt(pixelPoint);
                        float rim = max(max(holeAt(pixelPoint + float2(2, 0)), holeAt(pixelPoint - float2(2, 0))), max(holeAt(pixelPoint + float2(0, 2)), holeAt(pixelPoint - float2(0, 2))));
                        result *= 1 - saturate(rim - hole) * .38;
                        source.a *= 1 - hole;
                    }
                }
                #ifndef UNITY_COLORSPACE_GAMMA
                    result = GammaToLinearSpace(result);
                #endif
                return float4(result, source.a);
            }
            ENDCG
        }
    }
}
