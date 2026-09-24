Shader "Suturn/Stylized Ring"
{
    Properties
    {
        _Color ("Ring tint", Color) = (0.8, 0.71, 0.56, 0.8)
        _PlanetRadius ("Planet radius (object space)", Float) = 0.5
        _ShadowTint ("Planet shadow", Color) = (0.12, 0.12, 0.14, 1)
        _BacklitTint ("Unlit face", Color) = (0.45, 0.42, 0.40, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 edge : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float3 objectPosition : TEXCOORD0; };
            fixed4 _Color, _ShadowTint, _BacklitTint;
            float _PlanetRadius;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;

                // Edge-on, the annulus projects thinner than a pixel and rasterizes as a
                // flickering dotted line. Widen it on screen to MinPixels along the projected
                // ring normal and thin the alpha by the same factor, but keep a floor so the
                // edge-on rings still read as a line.
                const float MinPixels = 1.5;
                float3 camera = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                float facing = abs(normalize(camera - v.vertex.xyz).y);
                float scale = length(unity_ObjectToWorld._m00_m10_m20);
                float pixelWorld = o.pos.w * 2.0 / (_ScreenParams.y * UNITY_MATRIX_P._m11);
                float widthPixels = v.edge.y * scale * facing / pixelWorld;
                float grow = max(MinPixels - widthPixels, 0.0) * 0.5;
                float4 up = UnityObjectToClipPos(v.vertex + float4(0, v.edge.y * 0.01, 0, 0));
                float2 normalPixels = (up.xy / up.w - o.pos.xy / o.pos.w) * _ScreenParams.xy;
                normalPixels /= max(length(normalPixels), 1e-6);
                o.pos.xy += normalPixels * v.edge.x * grow * 2.0 / _ScreenParams.xy * o.pos.w;
                o.color.a *= max(widthPixels / max(widthPixels, MinPixels), 0.4);
                o.objectPosition = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Rings share the planet's object space and lie in its y = 0 plane.
                float3 p = i.objectPosition;
                float3 light = normalize(mul((float3x3)unity_WorldToObject, _WorldSpaceLightPos0.xyz));
                float3 camera = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;

                float along = dot(-p, light);
                float miss = length(p + light * along);
                float edge = max(fwidth(miss), 1e-6) * 0.5;
                float shadow = step(0, along) * (1.0 - smoothstep(_PlanetRadius - edge, _PlanetRadius + edge, miss));

                // Seen from the side the Sun does not reach, the ring glows dimmer. The blend is
                // soft near the ring plane so an equinox Sun does not flip it every frame.
                float backlit = smoothstep(0.02, -0.02, light.y * sign(camera.y));
                fixed3 color = i.color.rgb * lerp(1.0, _BacklitTint.rgb, backlit);
                color = lerp(color, i.color.rgb * _ShadowTint.rgb, shadow);
                return fixed4(color, i.color.a);
            }
            ENDCG
        }
    }
}
