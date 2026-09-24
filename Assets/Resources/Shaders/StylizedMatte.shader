Shader "Suturn/Stylized Matte"
{
    Properties
    {
        _Color ("Surface", Color) = (0.8, 0.82, 0.8, 1)
        _ShadeColor ("Shade tint", Color) = (0.24, 0.27, 0.29, 1)
        _ShadowLevel ("Shadow brightness", Range(0, 1)) = 0.45
        _MidLevel ("Middle brightness", Range(0, 1)) = 0.78
        _Edge ("Light threshold", Range(0, 1)) = 0.42
        _Softness ("Transition width", Range(0.01, 0.3)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Stepped fullforwardshadows addshadow noforwardadd
        #pragma target 3.0
        #include "Lighting.cginc"

        fixed4 _Color;
        fixed4 _ShadeColor;
        half _ShadowLevel;
        half _MidLevel;
        half _Edge;
        half _Softness;

        struct Input { float3 worldPos; };

        void surf(Input IN, inout SurfaceOutput o)
        {
            o.Albedo = _Color.rgb;
            o.Alpha = _Color.a;
        }

        half4 LightingStepped(SurfaceOutput s, half3 lightDir, half atten)
        {
            half light = saturate(dot(s.Normal, lightDir)) * atten;
            half mid = smoothstep(_Edge - _Softness, _Edge + _Softness, light);
            half high = smoothstep(0.72 - _Softness, 0.72 + _Softness, light);
            half level = lerp(_ShadowLevel, _MidLevel, mid);
            level = lerp(level, 1.0, high);
            half3 shade = lerp(s.Albedo * _ShadeColor.rgb, s.Albedo, level);
            return half4(shade * _LightColor0.rgb, s.Alpha);
        }
        ENDCG
    }

    FallBack "Diffuse"
}
