// Comfort vignette ring (ComfortVignette.cs): black, fading in beyond _ApertureDeg from the view
// centre. Drawn last, over everything, in both eyes (single-pass instanced stereo).
Shader "PermisVR/ComfortVignette"
{
    Properties
    {
        _ApertureDeg ("Clear half-angle (deg)", Float) = 70
        _FeatherDeg ("Soft edge (deg)", Float) = 14
    }
    SubShader
    {
        Tags { "Queue"="Overlay+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZTest Always
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            float _ApertureDeg;
            float _FeatherDeg;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float angle : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.angle = v.uv.x;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float alpha = saturate((i.angle - _ApertureDeg) / max(_FeatherDeg, 0.01));
                return fixed4(0, 0, 0, alpha);
            }
            ENDCG
        }
    }
}
