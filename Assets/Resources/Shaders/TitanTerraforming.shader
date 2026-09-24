Shader "Suturn/Titan Terraforming"
{
    Properties
    {
        _PatternScale ("Weather system scale", Range(1, 8)) = 2.0
        _BandStretch ("Longitude stretch", Range(1, 4)) = 3.0
        _Belts ("Zonal belts", Range(0, 0.2)) = 0.1
        _ClearThreshold ("Cloud density threshold", Range(0.1, 0.7)) = 0.42
        _RimPixels ("Atmosphere rim pixels", Range(0, 6)) = 2
        _SaturnFill ("Saturn night fill", Range(0, 1)) = 0.18
        _CloudDetail ("Cloud detail", Range(0, 1)) = 0.85
        _HazeWidth ("Thin cloud depth", Range(0.01, 0.2)) = 0.03
        _VeilStrength ("High cloud veils", Range(0, 1)) = 0.45
        _SaturnDirection ("Direction to Saturn", Vector) = (0, 0, 1, 0)
        _CloudLight ("Cloud light", Color) = (0.941, 0.776, 0.165, 1)
        _CloudMid ("Cloud mid", Color) = (0.784, 0.569, 0.110, 1)
        _CloudShadow ("Cloud shadow", Color) = (0.231, 0.188, 0.086, 1)
        _ClearDay ("Clear day", Color) = (0.373, 0.424, 0.118, 1)
        _ClearNight ("Clear night", Color) = (0.071, 0.086, 0.039, 1)
        _RimColor ("Atmosphere rim", Color) = (0.969, 0.835, 0.416, 1)
        _SaturnColor ("Saturn light", Color) = (0.38, 0.29, 0.12, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex SphereVert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            float _RimPixels;
            float _SaturnFill;
            float _HazeWidth;
            float _VeilStrength;
            float4 _SaturnDirection;
            fixed4 _CloudLight, _CloudMid, _CloudShadow;
            fixed4 _ClearDay, _ClearNight, _RimColor, _SaturnColor;
            // Set by TitanFieldBake through a property block: the fields below come from
            // these cubemaps instead of being computed per pixel.
            samplerCUBE _FieldsA, _FieldsB;
            float _Baked;
            float _FieldsSize;

            #include "TitanFields.cginc"
            #include "SuturnSphere.cginc"

            // Bilinear filtering is linear between texel centers, and a hard threshold turns
            // that into a polyline along every cloud edge. A cubic B-spline in four bilinear
            // taps keeps the edges smooth curves. Taps are offset along the face's own grid.
            float4 SampleSmooth(samplerCUBE cube, float3 p)
            {
                float3 a = abs(p);
                float3 n, u, v;
                if (a.x >= a.y && a.x >= a.z) { n = float3(sign(p.x), 0, 0); u = float3(0, 0, 1); v = float3(0, 1, 0); }
                else if (a.y >= a.z) { n = float3(0, sign(p.y), 0); u = float3(1, 0, 0); v = float3(0, 0, 1); }
                else { n = float3(0, 0, sign(p.z)); u = float3(1, 0, 0); v = float3(0, 1, 0); }
                float major = max(a.x, max(a.y, a.z));
                float2 c = (float2(dot(p, u), dot(p, v)) / major * 0.5 + 0.5) * _FieldsSize - 0.5;
                float2 i = floor(c), f = c - i;
                float2 f2 = f * f, f3 = f2 * f;
                float2 w0 = (1.0 - 3.0 * f + 3.0 * f2 - f3) / 6.0;
                float2 w1 = (4.0 - 6.0 * f2 + 3.0 * f3) / 6.0;
                float2 w2 = (1.0 + 3.0 * f + 3.0 * f2 - 3.0 * f3) / 6.0;
                float2 w3 = f3 / 6.0;
                float2 g0 = w0 + w1, g1 = w2 + w3;
                float2 t0 = ((i - 1.0 + w1 / g0) + 0.5) / _FieldsSize * 2.0 - 1.0;
                float2 t1 = ((i + 1.0 + w3 / g1) + 0.5) / _FieldsSize * 2.0 - 1.0;
                return g0.y * (g0.x * texCUBE(cube, n + t0.x * u + t0.y * v) + g1.x * texCUBE(cube, n + t1.x * u + t0.y * v)) +
                    g1.y * (g0.x * texCUBE(cube, n + t0.x * u + t1.y * v) + g1.x * texCUBE(cube, n + t1.x * u + t1.y * v));
            }

            fixed4 frag(SphereV2F i) : SV_Target
            {
                float pixelsOutside;
                float3 spherePoint = RaycastSphere(i, pixelsOutside);
                float3 normal = normalize(mul((float3x3)unity_ObjectToWorld, spherePoint));

                float density, medium, veilField, streakField;
                float3 relief;
                if (_Baked > 0.5)
                {
                    float4 a = SampleSmooth(_FieldsA, spherePoint);
                    density = a.r;
                    medium = a.g;
                    veilField = a.b;
                    streakField = a.a;
                    relief = texCUBE(_FieldsB, spherePoint).rgb * 2.0 - 1.0;
                }
                else
                {
                    TitanField field = TitanFieldAt(spherePoint, true);
                    density = field.density;
                    medium = field.medium;
                    veilField = field.veil;
                    streakField = field.streak;
                    // Density gradients suggest lit billows without extra geometry or
                    // repeatedly marching through a volume. Object space keeps it scale independent.
                    float3 dx = ddx(spherePoint), dy = ddy(spherePoint);
                    float3 tx = cross(dy, spherePoint), ty = cross(spherePoint, dx);
                    float determinant = dot(dx, tx);
                    float3 gradient = (tx * ddx(field.height) + ty * ddy(field.height)) /
                        (abs(determinant) < 1e-9 ? 1e-9 : determinant);
                    relief = ReliefVector(gradient);
                }
                float depth = density - _ClearThreshold;

                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz -
                    i.worldPosition * _WorldSpaceLightPos0.w);
                float sunlight = dot(normal, lightDirection);
                float midZone = HardStep(-0.1, sunlight);
                float lightZone = HardStep(0.5, sunlight);
                fixed3 cloudColor = lerp(lerp(_CloudShadow.rgb, _CloudMid.rgb, midZone),
                    _CloudLight.rgb, lightZone);
                fixed3 clearColor = lerp(lerp(_ClearNight.rgb, _ClearDay.rgb * 0.72, midZone),
                    _ClearDay.rgb, lightZone);

                float3 objectLight = normalize(mul((float3x3)unity_WorldToObject, lightDirection));
                float reliefLight = clamp(dot(relief, objectLight), -1.0, 1.0);
                float tone = 1.0 - 0.12 * (1.0 - HardStep(-0.15, reliefLight)) + 0.07 * HardStep(0.2, reliefLight);

                // One flat band of thin cloud along the margins, then solid cloud.
                float opacity = 0.5 * HardStep(0.0, depth) + 0.5 * HardStep(_HazeWidth, depth);

                float veil = HardStep(0.56, veilField) * _VeilStrength;
                tone -= 0.1 * HardStep(0.6, streakField);
                cloudColor *= lerp(1.0, tone, _CloudDetail);
                fixed3 atmosphere = clearColor * (0.8 + 0.2 * HardStep(0.48, medium));
                atmosphere = lerp(atmosphere, cloudColor, veil);
                fixed3 color = lerp(atmosphere, cloudColor, opacity);
                float saturnFacing = saturate(dot(normal, normalize(_SaturnDirection.xyz)));
                color += opacity * (1.0 - midZone) * saturnFacing * _SaturnFill * _SaturnColor.rgb;

                // The atmosphere thins toward the night side, where only Saturn lights it.
                float rimLight = max(saturate(sunlight + 0.5), saturnFacing * _SaturnFill);
                if (pixelsOutside > 0)
                {
                    clip(_RimPixels * rimLight - pixelsOutside);
                    return fixed4(_RimColor.rgb, 1);
                }

                return fixed4(color, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
