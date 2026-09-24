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

            float _PatternScale;
            float _BandStretch;
            float _Belts;
            float _ClearThreshold;
            float _RimPixels;
            float _SaturnFill;
            float _CloudDetail;
            float _HazeWidth;
            float _VeilStrength;
            float4 _SaturnDirection;
            fixed4 _CloudLight, _CloudMid, _CloudShadow;
            fixed4 _ClearDay, _ClearNight, _RimColor, _SaturnColor;

            #include "SuturnNoise.cginc"
            #include "SuturnSphere.cginc"

            float Worley(float3 p)
            {
                float3 c = floor(p), f = frac(p);
                float nearest = 10;
                [unroll] for (int z = -1; z <= 1; z++)
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float3 o = float3(x, y, z);
                    float3 d = o + Hash33(c + o) - f;
                    nearest = min(nearest, dot(d, d));
                }
                return saturate(sqrt(nearest));
            }

            float3 Rotate(float3 p, float3 axis, float angle)
            {
                float s, c;
                sincos(angle, s, c);
                return p * c + cross(axis, p) * s + axis * dot(axis, p) * (1.0 - c);
            }

            float3 Vortex(float3 p, float3 axis, float strength, float width)
            {
                axis = normalize(axis);
                float radius = 1.0 - dot(axis, p);
                return Rotate(p, axis, strength * exp(-radius / width));
            }

            float3 FlowCoordinates(float3 p)
            {
                // Latitude shear and localized vortices organize all noise scales
                // into the same weather systems. Rotations keep points on the sphere.
                p = Rotate(p, float3(0, 1, 0), 0.3 * sin(p.y * 4.0) + p.y * 0.35);
                p = Vortex(p, float3(-0.45, 0.35, -0.82), 2.1, 0.16);
                p = Vortex(p, float3(0.65, -0.35, -0.68), -1.8, 0.15);
                p = Vortex(p, float3(0.25, 0.55, 0.8), 1.7, 0.19);
                return p;
            }

            fixed4 frag(SphereV2F i) : SV_Target
            {
                float pixelsOutside;
                float3 spherePoint = RaycastSphere(i, pixelsOutside);
                float3 normal = normalize(mul((float3x3)unity_ObjectToWorld, spherePoint));
                // Stretch along longitude without raising the latitude frequency,
                // so systems become banded rather than finer.
                float stretchRoot = sqrt(_BandStretch);
                float3 stretch = float3(1.0 / stretchRoot, stretchRoot, 1.0 / stretchRoot);
                float3 q = FlowCoordinates(spherePoint) * _PatternScale * stretch;
                float large = Perlin(q + 11.3);
                float3 local = lerp(spherePoint * _PatternScale * stretch, q, 0.55);
                float3 eddy = local * 2.8;
                eddy += 0.3 * sin(eddy.yzx * 1.7 + sin(eddy.zxy * 2.1));
                float medium = Perlin(eddy + 23.7);
                float small = Perlin(eddy * 2.17 + 37.1);
                float footprint = max(length(ddx(local)), length(ddy(local)));
                float fineFade = 1.0 - smoothstep(0.035, 0.12, footprint);
                float fine = 0.5;
                [branch] if (fineFade > 0)
                    fine = lerp(0.5, Perlin(local * 15.7 + 51.3), fineFade);
                float density = large * 0.45 + medium * 0.35 + small * 0.16 + fine * 0.04;
                // Zonal belts, bent by the large systems so they do not read as ruled stripes.
                density += _Belts * sin(spherePoint.y * 6.0 + (large - 0.5) * 4.0);
                float margin = 1.0 - smoothstep(_ClearThreshold + 0.06,
                    _ClearThreshold + 0.22, density);
                float erosionWeight = 0.10 * _CloudDetail * margin;
                [branch] if (erosionWeight > 0)
                    density -= Worley(eddy * 2.2 + 7.9) * erosionWeight;
                float depth = density - _ClearThreshold;
                // Dense interiors get billow relief too, while the mask is eroded
                // mainly at margins. Fine features fade before becoming subpixel noise.
                float height = large * 0.52 + medium * 0.32 + small * 0.16;

                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz -
                    i.worldPosition * _WorldSpaceLightPos0.w);
                float sunlight = dot(normal, lightDirection);
                float midZone = HardStep(-0.1, sunlight);
                float lightZone = HardStep(0.5, sunlight);
                fixed3 cloudColor = lerp(lerp(_CloudShadow.rgb, _CloudMid.rgb, midZone),
                    _CloudLight.rgb, lightZone);
                fixed3 clearColor = lerp(lerp(_ClearNight.rgb, _ClearDay.rgb * 0.72, midZone),
                    _ClearDay.rgb, lightZone);

                // Density gradients suggest lit billows without extra geometry or
                // repeatedly marching through a volume. Use object space for scale independence.
                float3 dx = ddx(spherePoint), dy = ddy(spherePoint);
                float3 tx = cross(dy, spherePoint), ty = cross(spherePoint, dx);
                float determinant = dot(dx, tx);
                float3 gradient = (tx * ddx(height) + ty * ddy(height)) /
                    (abs(determinant) < 1e-9 ? 1e-9 : determinant);
                float3 objectLight = normalize(mul((float3x3)unity_WorldToObject, lightDirection));
                // Gradient magnitude grows with noise frequency; normalize to the default scale of 3.
                float relief = clamp(dot(gradient, objectLight) * 0.24 / _PatternScale, -1.0, 1.0);
                float tone = 1.0 - 0.12 * (1.0 - HardStep(-0.15, relief)) + 0.07 * HardStep(0.2, relief);

                // One flat band of thin cloud along the margins, then solid cloud.
                float opacity = 0.5 * HardStep(0.0, depth) + 0.5 * HardStep(_HazeWidth, depth);

                // A higher, stretched layer crosses clear regions instead of tracing
                // their perimeter. Its own field produces interrupted cirrus filaments.
                float cirrusField = Perlin(q * float3(3.2, 4.0, 3.2) + 63.8);
                float veil = HardStep(0.56, cirrusField + (medium - 0.5) * 0.14) * _VeilStrength;
                // Inside the cloud body the same flow field leaves darker ochre streaks.
                tone -= 0.1 * HardStep(0.6, cirrusField + (small - 0.5) * 0.2);
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
