Shader "Suturn/Space Sky"
{
    Properties
    {
        _SpaceColor ("Space", Color) = (0.016, 0.027, 0.047, 1)
        _StarColor ("Star", Color) = (0.93, 0.95, 1, 1)
        _WarmStarColor ("Warm star", Color) = (1, 0.86, 0.66, 1)
        _StarBrightness ("Star brightness", Range(0, 1)) = 0.8
        _SunColor ("Sun disc", Color) = (1, 0.97, 0.88, 1)
        _HaloColor ("Sun glare", Color) = (1, 0.86, 0.55, 1)
        _SunPixels ("Minimum sun radius pixels", Range(1, 30)) = 6
        _HaloPixels ("Glare ring pixels", Range(0, 60)) = 14
        _SunDirection ("Direction to Sun", Vector) = (0, 0, 1, 0)
        _SunAngularRadius ("Sun angular radius", Float) = 0.000487
        _SkyX ("Inertial X", Vector) = (1, 0, 0, 0)
        _SkyY ("Inertial Y", Vector) = (0, 1, 0, 0)
        _SkyZ ("Inertial Z", Vector) = (0, 0, 1, 0)
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "SuturnNoise.cginc"

            fixed4 _SpaceColor, _StarColor, _WarmStarColor, _SunColor, _HaloColor;
            float _StarBrightness, _SunPixels, _HaloPixels, _SunAngularRadius;
            float4 _SunDirection, _SkyX, _SkyY, _SkyZ;

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(vertex);
                o.direction = vertex.xyz;
                return o;
            }

            // Stars sit in cells of a cube wrapped around the sky. Size is measured in screen
            // pixels, so they stay crisp flat dots at any field of view.
            float2 StarLayer(float3 direction, float cells, float density, float seed, float pixelAngle)
            {
                float3 a = abs(direction);
                float major = max(a.x, max(a.y, a.z));
                float face = a.x == major ? (direction.x > 0 ? 0 : 1) :
                    a.y == major ? (direction.y > 0 ? 2 : 3) : (direction.z > 0 ? 4 : 5);
                float2 uv = a.x == major ? direction.yz : a.y == major ? direction.xz : direction.xy;
                float2 cell = (uv / major * 0.5 + 0.5) * cells;
                float3 h = Hash33(float3(floor(cell), face + seed * 6.0));
                if (h.z > density) return 0;
                float2 offset = frac(cell) - (0.2 + 0.6 * h.xy);
                // Angle of one cell unit shrinks toward cube corners as major^2.
                float pixels = length(offset) * 2.0 / cells * major * major / pixelAngle;
                float bright = h.x * h.x * h.x * h.x;
                float radius = lerp(0.6, 1.6, bright);
                float coverage = saturate(radius - pixels + 0.5);
                return float2(coverage * lerp(0.25, 1.0, bright), h.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 direction = normalize(i.direction);
                float pixelAngle = max(max(length(ddx(direction)), length(ddy(direction))), 1e-6);
                float3 inertial = float3(dot(direction, _SkyX.xyz), dot(direction, _SkyY.xyz),
                    dot(direction, _SkyZ.xyz));

                fixed3 color = _SpaceColor.rgb;
                float2 faint = StarLayer(inertial, 110, 0.3, 0, pixelAngle);
                float2 bright = StarLayer(inertial, 36, 0.3, 1, pixelAngle);
                float2 star = faint.x * 0.5 > bright.x ? float2(faint.x * 0.5, faint.y) : bright;
                fixed3 starColor = star.y > 0.8 ? _WarmStarColor.rgb : _StarColor.rgb;
                color = lerp(color, starColor, star.x * _StarBrightness);

                // The true disc is about a pixel across from Saturn, so it has a minimum
                // on-screen size; glare is two flat rings rather than a gradient.
                float3 sun = normalize(_SunDirection.xyz);
                float sunPixels = length(direction - sun) / pixelAngle;
                float sunRadius = max(_SunAngularRadius / pixelAngle, _SunPixels);
                float inner = saturate(sunRadius + _HaloPixels - sunPixels + 0.5);
                float outer = saturate(sunRadius + _HaloPixels * 2.5 - sunPixels + 0.5);
                color = lerp(color, _HaloColor.rgb, outer * 0.12 + inner * 0.18);
                color = lerp(color, _SunColor.rgb, saturate(sunRadius - sunPixels + 0.5));

                return fixed4(color, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
