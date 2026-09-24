Shader "Suturn/Saturn"
{
    Properties
    {
        _Pale ("Equatorial zone", Color) = (0.925, 0.867, 0.706, 1)
        _Cream ("Zone", Color) = (0.855, 0.769, 0.580, 1)
        _Tan ("Belt", Color) = (0.749, 0.616, 0.420, 1)
        _Ochre ("Dark belt", Color) = (0.612, 0.478, 0.306, 1)
        _Polar ("Polar cap", Color) = (0.557, 0.588, 0.561, 1)
        _Hexagon ("North hexagon", Color) = (0.420, 0.475, 0.478, 1)
        _MidTint ("Terminator tint", Color) = (0.80, 0.70, 0.60, 1)
        _NightTint ("Night tint", Color) = (0.10, 0.11, 0.14, 1)
        _Turbulence ("Band turbulence", Range(0, 0.05)) = 0.018
        _Streaks ("Band streaks", Range(0, 0.2)) = 0.07
        _RingShadow ("Ring shadow", Range(0, 1)) = 0.85
        _RimPixels ("Atmosphere rim pixels", Range(0, 6)) = 1.5
        _RimColor ("Atmosphere rim", Color) = (0.910, 0.851, 0.690, 1)
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

            fixed4 _Pale, _Cream, _Tan, _Ochre, _Polar, _Hexagon, _MidTint, _NightTint, _RimColor;
            float _Turbulence, _Streaks, _RingShadow, _RimPixels;

            #include "SuturnNoise.cginc"
            #include "SuturnSphere.cginc"

            // Opacity of the ring whose shadow falls at radius r, in planet radii.
            // Edges follow StylizedRing (NASA NSSDCA); values track the ring materials.
            float RingOpacity(float r)
            {
                return 0.26 * (HardStep(1.282, r) - HardStep(1.579, r)) +
                    0.72 * (HardStep(1.579, r) - HardStep(2.018, r)) +
                    0.46 * (HardStep(2.101, r) - HardStep(2.349, r));
            }

            fixed4 frag(SphereV2F i) : SV_Target
            {
                float pixelsOutside;
                float3 p = RaycastSphere(i, pixelsOutside);
                float3 normal = normalize(mul((float3x3)unity_ObjectToWorld, p));
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float sunlight = dot(normal, lightDirection);

                // Latitude bands with wavy edges: the warp runs along longitude, so the
                // belts keep their zonal look instead of breaking into blobs.
                float warp = (Perlin(p * float3(5.0, 34.0, 5.0) + 3.1) - 0.5) * 2.0 +
                    (Perlin(p * float3(14.0, 90.0, 14.0) + 8.7) - 0.5);
                float latitude = p.y + warp * _Turbulence;
                float band = 0.5 + 0.28 * sin(latitude * 23.0 + 0.6) +
                    0.14 * sin(latitude * 57.0 + 1.9) + 0.06 * sin(latitude * 131.0);
                fixed3 color = lerp(_Cream.rgb, _Tan.rgb, HardStep(0.55, band));
                color = lerp(color, _Ochre.rgb, HardStep(0.74, band));
                color = lerp(color, _Pale.rgb, 1.0 - HardStep(0.33, band));
                color = lerp(_Pale.rgb, color, HardStep(0.09, abs(latitude)));

                float streak = Perlin(p * float3(9.0, 160.0, 9.0) + 17.0);
                color *= 1.0 - _Streaks * HardStep(0.62, streak);

                color = lerp(color, _Polar.rgb, HardStep(0.9, abs(latitude)));
                // The north polar hexagon, near 78 degrees latitude.
                float angle = atan2(p.z, p.x);
                float sector = fmod(angle + UNITY_PI * 7.0, UNITY_PI / 3.0) - UNITY_PI / 6.0;
                float hexRadius = length(p.xz) * cos(sector) / cos(UNITY_PI / 6.0);
                float north = HardStep(0.0, p.y);
                color = lerp(color, _Hexagon.rgb, north * (1.0 - HardStep(0.21, hexRadius)));
                color = lerp(color, _Polar.rgb, north * (1.0 - HardStep(0.05, length(p.xz))));

                float3 objectLight = normalize(mul((float3x3)unity_WorldToObject, lightDirection));
                // Rings shadow the hemisphere on the other side of the ring plane from the Sun.
                float lightHeight = abs(objectLight.y) < 1e-4 ? 1e-4 : objectLight.y;
                float3 hit = p - objectLight * (p.y / lightHeight);
                float ringShadow = RingOpacity(length(hit.xz)) * _RingShadow * step(objectLight.y * p.y, 0);

                float midZone = HardStep(-0.05, sunlight);
                float lightZone = HardStep(0.35, sunlight);
                fixed3 lit = lerp(color * _MidTint.rgb, color, lightZone);
                lit = lerp(lit, color * _NightTint.rgb, ringShadow);
                fixed3 shaded = lerp(color * _NightTint.rgb, lit, midZone);

                float rimLight = saturate(sunlight + 0.5);
                if (pixelsOutside > 0)
                {
                    clip(_RimPixels * rimLight - pixelsOutside);
                    return fixed4(_RimColor.rgb, 1);
                }
                return fixed4(shaded, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
