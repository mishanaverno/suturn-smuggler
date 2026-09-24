Shader "Suturn/Stylized Body"
{
    Properties
    {
        _Color ("Base", Color) = (0.72, 0.7, 0.63, 1)
        _BandColor ("Latitude band", Color) = (0.56, 0.49, 0.39, 1)
        _BandStrength ("Band strength", Range(0, 1)) = 0
        _ShadeColor ("Night side", Color) = (0.055, 0.07, 0.09, 1)
        _Edge ("Terminator", Range(-0.2, 0.5)) = 0.08
        _Softness ("Terminator width", Range(0.01, 0.3)) = 0.11
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf DayNight vertex:vert fullforwardshadows addshadow noforwardadd
        #pragma target 3.0
        #include "Lighting.cginc"

        fixed4 _Color;
        fixed4 _BandColor;
        fixed4 _ShadeColor;
        half _BandStrength;
        half _Edge;
        half _Softness;

        struct Input { float3 localPosition; };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.localPosition = v.vertex.xyz;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            // Object-space latitude remains stable while the camera and sun move.
            half latitude = IN.localPosition.y * 2.0;
            half broad = sin(latitude * 19.0) * 0.5 + 0.5;
            half narrow = sin(latitude * 61.0 + 0.8) * 0.5 + 0.5;
            half bands = smoothstep(0.42, 0.62, broad * 0.7 + narrow * 0.3);
            o.Albedo = lerp(_Color.rgb, lerp(_Color.rgb, _BandColor.rgb, bands), _BandStrength);
            o.Alpha = 1;
        }

        half4 LightingDayNight(SurfaceOutput s, half3 lightDir, half atten)
        {
            half sunlight = dot(s.Normal, lightDir) * atten;
            half day = smoothstep(_Edge - _Softness, _Edge + _Softness, sunlight);
            half face = lerp(0.77, 1.0, smoothstep(0.25, 0.75, sunlight));
            half3 color = lerp(_ShadeColor.rgb, s.Albedo * _LightColor0.rgb * face, day);
            return half4(color, s.Alpha);
        }
        ENDCG
    }

    FallBack "Diffuse"
}
