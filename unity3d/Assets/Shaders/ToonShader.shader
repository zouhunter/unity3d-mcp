Shader "Custom/ToonShader"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _RampTex ("Ramp Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.005
        _Steps ("Shading Steps", Range(1, 10)) = 3
        _ToonEffect ("Toon Effect", Range(0, 1)) = 0.5
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "LightMode"="ForwardBase"
        }
        LOD 200
        
        // Main Pass
        Pass
        {
            Name "TOON"
            Tags { "LightMode"="ForwardBase" }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                SHADOW_COORDS(3)
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _RampTex;
            fixed4 _Color;
            float _Steps;
            float _ToonEffect;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                TRANSFER_SHADOW(o);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Sample main texture
                fixed4 tex = tex2D(_MainTex, i.uv) * _Color;
                
                // Calculate lighting
                float3 normal = normalize(i.worldNormal);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                
                // Calculate dot product for diffuse lighting
                float NdotL = dot(normal, lightDir);
                
                // Apply toon shading with steps
                float toonDiffuse = floor(NdotL * _Steps) / _Steps;
                toonDiffuse = lerp(NdotL, toonDiffuse, _ToonEffect);
                toonDiffuse = max(0, toonDiffuse);
                
                // Sample ramp texture for additional toon effect
                float2 rampUV = float2(toonDiffuse, 0.5);
                fixed3 ramp = tex2D(_RampTex, rampUV).rgb;
                
                // Calculate shadows
                fixed shadow = SHADOW_ATTENUATION(i);
                
                // Final color calculation
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz;
                fixed3 diffuse = _LightColor0.rgb * toonDiffuse * shadow;
                
                fixed3 finalColor = tex.rgb * (ambient + diffuse) * ramp;
                
                return fixed4(finalColor, tex.a);
            }
            ENDCG
        }
        
        // Outline Pass
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="Always" }
            Cull Front
            ZWrite On
            ColorMask RGB
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
            };
            
            float _OutlineWidth;
            fixed4 _OutlineColor;
            
            v2f vert (appdata v)
            {
                v2f o;
                
                // Calculate outline by expanding vertices along normals
                float3 norm = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
                float2 offset = TransformViewToProjection(norm.xy);
                
                o.pos = UnityObjectToClipPos(v.vertex);
                o.pos.xy += offset * _OutlineWidth * o.pos.z;
                
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }
    
    SubShader
    {
        // Fallback for older graphics cards
        Tags { "RenderType"="Opaque" }
        LOD 100
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                return col;
            }
            ENDCG
        }
    }
    
    FallBack "Diffuse"
}