Shader "Bj/Skybox6SidedSphere"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Int) = 0
        _Tint("Tint Color", Color) = (1, 1, 1, 1)
        [Gamma] _Exposure("Exposure", Range(0, 8)) = 1
        [Enum(WorldView, 0, ObjectSpace, 1)] _SampleSpace("Sample Space", Float) = 1
        _Rotation("Rotation", Range(0, 360)) = 0
        _Pitch("Vertical Rotation", Range(-90, 90)) = 0
        [NoScaleOffset] _FrontTex("Front (+Z)", 2D) = "grey" {}
        [NoScaleOffset] _BackTex("Back (-Z)", 2D) = "grey" {}
        [NoScaleOffset] _LeftTex("Left (-X)", 2D) = "grey" {}
        [NoScaleOffset] _RightTex("Right (+X)", 2D) = "grey" {}
        [NoScaleOffset] _UpTex("Up (+Y)", 2D) = "grey" {}
        [NoScaleOffset] _DownTex("Down (-Y)", 2D) = "grey" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry-100"
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
        }

        Cull [_Cull]
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "SkySphere"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Exposure;
                half _SampleSpace;
                half _Rotation;
                half _Pitch;
            CBUFFER_END

            TEXTURE2D(_FrontTex);
            SAMPLER(sampler_FrontTex);
            TEXTURE2D(_BackTex);
            SAMPLER(sampler_BackTex);
            TEXTURE2D(_LeftTex);
            SAMPLER(sampler_LeftTex);
            TEXTURE2D(_RightTex);
            SAMPLER(sampler_RightTex);
            TEXTURE2D(_UpTex);
            SAMPLER(sampler_UpTex);
            TEXTURE2D(_DownTex);
            SAMPLER(sampler_DownTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            float3 RotateAroundY(float3 dir, half degrees)
            {
                half radians = degrees * 0.01745329252;
                half s = sin(radians);
                half c = cos(radians);
                return float3(
                    dir.x * c - dir.z * s,
                    dir.y,
                    dir.x * s + dir.z * c
                );
            }

            float3 RotateAroundX(float3 dir, half degrees)
            {
                half radians = degrees * 0.01745329252;
                half s = sin(radians);
                half c = cos(radians);
                return float3(
                    dir.x,
                    dir.y * c - dir.z * s,
                    dir.y * s + dir.z * c
                );
            }

            half4 SampleSixSided(float3 dir)
            {
                dir = normalize(dir);
                float3 absDir = abs(dir);
                float2 uv;

                if (absDir.x >= absDir.y && absDir.x >= absDir.z)
                {
                    if (dir.x > 0.0)
                    {
                        uv = float2(-dir.z, dir.y) / absDir.x;
                        uv = uv * 0.5 + 0.5;
                        return SAMPLE_TEXTURE2D(_LeftTex, sampler_LeftTex, uv);
                    }

                    uv = float2(dir.z, dir.y) / absDir.x;
                    uv = uv * 0.5 + 0.5;
                    return SAMPLE_TEXTURE2D(_RightTex, sampler_RightTex, uv);
                }

                if (absDir.y >= absDir.x && absDir.y >= absDir.z)
                {
                    if (dir.y > 0.0)
                    {
                        uv = float2(dir.x, -dir.z) / absDir.y;
                        uv = uv * 0.5 + 0.5;
                        return SAMPLE_TEXTURE2D(_UpTex, sampler_UpTex, uv);
                    }

                    uv = float2(dir.x, dir.z) / absDir.y;
                    uv = uv * 0.5 + 0.5;
                    return SAMPLE_TEXTURE2D(_DownTex, sampler_DownTex, uv);
                }

                if (dir.z > 0.0)
                {
                    uv = float2(dir.x, dir.y) / absDir.z;
                    uv = uv * 0.5 + 0.5;
                    return SAMPLE_TEXTURE2D(_FrontTex, sampler_FrontTex, uv);
                }

                uv = float2(-dir.x, dir.y) / absDir.z;
                uv = uv * 0.5 + 0.5;
                return SAMPLE_TEXTURE2D(_BackTex, sampler_BackTex, uv);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = _SampleSpace > 0.5
                    ? input.positionOS
                    : input.positionWS - _WorldSpaceCameraPos.xyz;
                dir = RotateAroundX(dir, _Pitch);
                dir = RotateAroundY(dir, _Rotation);

                half4 color = SampleSixSided(dir);
                color.rgb *= _Tint.rgb * _Exposure;
                color.a = 1.0;
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
