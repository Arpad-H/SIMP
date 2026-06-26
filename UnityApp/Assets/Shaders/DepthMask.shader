// Invisible depth-only occluder. Renders just BEFORE the trunk and writes depth
// without writing color, so the trunk fails its depth test wherever this box sits
// in front of it -> the trunk is "removed" there and you see the scene behind.
// CutProgress grows a cube using this material to carve the chainsaw kerf.
// Material-agnostic: it never touches the trunk's own (AllIn13DShader) material.
Shader "SIMP/DepthMask"
{
    SubShader
    {
        // Queue is overridden from C# to (trunkQueue - 1) so it always draws first.
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-1" }

        Pass
        {
            Name "DepthMask"
            ColorMask 0      // write no color...
            ZWrite On        // ...only depth
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
