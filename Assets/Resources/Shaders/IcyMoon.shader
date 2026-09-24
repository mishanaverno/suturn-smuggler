Shader "Suturn/Icy Moon"
{
    Properties
    {
        _Base ("Surface", Color) = (0.78, 0.78, 0.76, 1)
        _Dark ("Crater floor", Color) = (0.58, 0.58, 0.56, 1)
        _CraterScale ("Crater scale", Range(1, 12)) = 5
        _CraterCoverage ("Crater coverage", Range(0, 1)) = 0.6
        _Hemisphere ("Leading (+) / trailing (-) tint", Range(-1, 1)) = 0
        _HemisphereColor ("Hemisphere tint", Color) = (0.23, 0.16, 0.12, 1)
        _BigCrater ("Big crater (object direction, w = radius)", Vector) = (0, 0, 0, 0)
        _Stripes ("South polar stripes", Range(0, 1)) = 0
        _StripeColor ("Stripes", Color) = (0.50, 0.66, 0.71, 1)
        _MidTint ("Terminator tint", Color) = (0.72, 0.74, 0.78, 1)
        _NightTint ("Night tint", Color) = (0.07, 0.08, 0.10, 1)
        _Leading ("Direction of motion", Vector) = (1, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex SphereVert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            fixed4 _Base, _Dark, _HemisphereColor, _StripeColor, _MidTint, _NightTint;
            float _CraterScale, _CraterCoverage, _Hemisphere, _Stripes;
            float4 _BigCrater, _Leading;
            // Airless: no limb haze, the disc just ends.
            static const float _RimPixels = 0;

            #include "SuturnNoise.cginc"
            #include "SuturnSphere.cginc"

            // Nearest crater around p: x = distance from its center in crater radii,
            // yzw = direction from the crater center to p.
            // Centers sit 0.15..0.85 into their cell and radii reach 0.55, so a crater from
            // the cell beyond the far side of p is at least 0.65 away and never covers it:
            // eight cells toward p give the same result as the full 27.
            float4 Crater(float3 p, float scale, float coverage)
            {
                float3 q = p * scale;
                float3 c = floor(q), f = frac(q);
                float3 side = step(0.5, f) * 2.0 - 1.0;
                float4 best = float4(2, 0, 0, 0);
                [unroll] for (int z = 0; z <= 1; z++)
                [unroll] for (int y = 0; y <= 1; y++)
                [unroll] for (int x = 0; x <= 1; x++)
                {
                    float3 o = float3(x, y, z) * side;
                    float3 h = Hash33(c + o);
                    float3 offset = f - (o + 0.15 + 0.7 * h);
                    float radius = lerp(0.2, 0.55, h.x * h.x);
                    float t = length(offset) / radius + (h.z > coverage ? 10 : 0);
                    if (t < best.x) best = float4(t, offset);
                }
                return best;
            }

            // Flat crater: dark floor, bright rim, and the inner wall on the Sun's side in shadow.
            fixed3 Paint(fixed3 color, float4 crater, float3 light, float edge)
            {
                float t = crater.x;
                float inside = 1.0 - HardStep(1.0, t);
                float wall = inside * HardStep(0.35, t) *
                    HardStep(0.3, dot(normalize(crater.yzw + 1e-6), light));
                color = lerp(color, _Dark.rgb, inside * (1.0 - HardStep(0.7, t)) * 0.6);
                color = lerp(color, saturate(color * 1.12), inside * HardStep(0.82, t));
                return lerp(color, color * _MidTint.rgb * 0.75, wall * edge);
            }

            fixed4 frag(SphereV2F i) : SV_Target
            {
                float pixelsOutside;
                float3 p = RaycastSphere(i, pixelsOutside);
                clip(-pixelsOutside);
                float3 normal = normalize(mul((float3x3)unity_ObjectToWorld, p));
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float3 light = normalize(mul((float3x3)unity_WorldToObject, lightDirection));
                float sunlight = dot(normal, lightDirection);
                float midZone = HardStep(-0.05, sunlight);
                float lightZone = HardStep(0.35, sunlight);

                fixed3 color = _Base.rgb;
                // Tidally locked moons show one face to their motion: Iapetus is dark ahead,
                // Dione and Rhea behind. The boundary is irregular, not a great circle.
                float3 leading = normalize(mul((float3x3)unity_WorldToObject, _Leading.xyz));
                float ahead = dot(p, leading) * sign(_Hemisphere) +
                    (Perlin(p * 3.0 + 5.0) - 0.5) * 0.5;
                color = lerp(color, _HemisphereColor.rgb, HardStep(0.1, ahead) * abs(_Hemisphere));

                color = Paint(color, Crater(p, _CraterScale, _CraterCoverage), light, midZone);
                color = Paint(color, Crater(p, _CraterScale * 2.6, _CraterCoverage * 0.7), light, midZone);
                if (_BigCrater.w > 0)
                {
                    float3 center = normalize(_BigCrater.xyz);
                    float3 offset = p - center;
                    float4 big = float4(length(offset) / _BigCrater.w, offset);
                    color = Paint(color, big, light, midZone);
                    color = lerp(color, saturate(_Base.rgb * 1.1), 1.0 - HardStep(0.12, big.x));
                }

                // Enceladus: parallel fractures across the south pole.
                float3 s = float3(p.x * 0.88 + p.z * 0.47, p.y, p.z * 0.88 - p.x * 0.47);
                float stripe = HardStep(0.42, abs(frac(s.x * 11.0) - 0.5)) *
                    (1.0 - HardStep(0.2, abs(s.x))) * (1.0 - HardStep(0.3, abs(s.z))) *
                    (1.0 - HardStep(-0.8, s.y));
                color = lerp(color, _StripeColor.rgb, stripe * _Stripes);

                fixed3 lit = lerp(color * _MidTint.rgb, color, lightZone);
                return fixed4(lerp(color * _NightTint.rgb, lit, midZone), 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
