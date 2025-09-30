Shader "Custom/MirrorShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ReflectionTex ("Reflection", 2D) = "black" {}
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.8
        _Smoothness ("Smoothness", Range(0, 1)) = 0.9
        _Metallic ("Metallic", Range(0, 1)) = 1.0
        _Color ("Tint Color", Color) = (1,1,1,1)
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 300
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
                float fogCoord : TEXCOORD5;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _ReflectionStrength;
                float _Smoothness;
                float _Metallic;
                float4 _Color;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                
                output.positionHCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.screenPos = ComputeScreenPos(output.positionHCS);
                output.fogCoord = ComputeFogFactor(output.positionHCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // 基础颜色
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                
                // 法线和视角方向
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                
                // 计算反射向量
                float3 reflectionVector = reflect(-viewDirWS, normalWS);
                
                // 计算屏幕空间UV用于反射采样
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                
                // 添加一些扰动来模拟反射效果
                float2 distortion = normalWS.xy * 0.1;
                screenUV += distortion;
                
                // 采样反射贴图（这里使用主贴图作为简单的环境反射）
                half4 reflectionColor = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, screenUV);
                
                // 如果没有反射贴图，使用天空盒颜色模拟
                if (reflectionColor.a < 0.1)
                {
                    // 使用反射向量的y分量来模拟天空颜色
                    float skyFactor = saturate(reflectionVector.y);
                    reflectionColor = lerp(half4(0.2, 0.3, 0.6, 1), half4(0.8, 0.9, 1.0, 1), skyFactor);
                }
                
                // 计算菲涅尔反射
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), 2.0);
                fresnel = lerp(0.2, 1.0, fresnel);
                
                // 混合基础颜色和反射
                half3 finalColor = lerp(albedo.rgb, reflectionColor.rgb, _ReflectionStrength * fresnel * _Metallic);
                
                // 添加镜面高光
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float3 halfVector = normalize(lightDir + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfVector));
                float specular = pow(NdotH, _Smoothness * 128.0);
                
                finalColor += mainLight.color * specular * _Metallic;
                
                // 应用雾效
                finalColor = MixFog(finalColor, input.fogCoord);
                
                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }
        
        // Shadow Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                // Simple shadow casting without bias for compatibility
                float3 worldPos = TransformObjectToWorld(v.vertex.xyz);
                o.pos = TransformWorldToHClip(worldPos);
                
                // Clamp to near plane
                #if UNITY_REVERSED_Z
                    o.pos.z = max(o.pos.z, o.pos.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    o.pos.z = min(o.pos.z, o.pos.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                
                return o;
            }
            
            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return 0;
            }
            ENDHLSL
        }
    }
    
    Fallback "Diffuse"
}
