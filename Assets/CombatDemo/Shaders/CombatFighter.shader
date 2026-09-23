Shader "Milkfrog/Combat Fighter"
{
    Properties
    {
        _BaseColor("Faction Color", Color) = (0.6,0.7,0.8,1)
        _BaseMap("Base Map", 2D) = "white" {}
        [HDR] _FlashColor("Impact Color", Color) = (1.6,1.6,1.6,1)
        _FlashAmount("Impact Amount", Range(0,1)) = 0
        _Cutoff("Cutoff", Float) = 0.5
        _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "CombatForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _FlashColor;
                float4 _BaseMap_ST;
                float _FlashAmount, _Cutoff, _Cull;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS; output.positionWS = p.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS); return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 ambient = max(SampleSH(n), half3(.19,.19,.19));
                half3 lit = _BaseColor.rgb * (ambient + light.color * saturate(dot(n,light.direction)) * light.shadowAttenuation);
                half rim = pow(1 - saturate(dot(n, GetWorldSpaceNormalizeViewDir(input.positionWS))), 3);
                // Flash is applied after lighting: the parried attacker reads white even on its shadowed side.
                return half4(lerp(lit, _FlashColor.rgb + rim * .35, saturate(_FlashAmount)), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
