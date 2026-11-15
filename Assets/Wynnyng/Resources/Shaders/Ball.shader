Shader "Wynnyng/Ball"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.96,0.95,0.86,1)
        _PanelColor ("Panel Color", Color) = (0.08,0.10,0.12,1)
        _PanelFrequency ("Pattern Frequency", Float) = 8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Ball"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _PanelColor;
                float _PanelFrequency;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 cell = floor(input.uv * _PanelFrequency);
                half pattern = fmod(cell.x + cell.y, 2.0);
                half3 color = lerp(_BaseColor.rgb, _PanelColor.rgb, pattern);
                Light light = GetMainLight();
                half diffuse = saturate(dot(normalize(input.normalWS), light.direction));
                half3 ambient = SampleSH(normalize(input.normalWS));
                return half4(color * (ambient + light.color * diffuse), 1);
            }
            ENDHLSL
        }
    }
}
