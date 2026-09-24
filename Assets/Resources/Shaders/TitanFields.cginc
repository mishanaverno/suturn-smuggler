#ifndef TITAN_FIELDS_INCLUDED
#define TITAN_FIELDS_INCLUDED

// Titan's cloud fields depend only on the point of the sphere and the shape parameters,
// not on light or camera. That is what lets them be baked once into cubemaps.

#include "SuturnNoise.cginc"

float _PatternScale;
float _BandStretch;
float _Belts;
float _ClearThreshold;
float _CloudDetail;

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

struct TitanField
{
    float density;
    float height;
    float medium;
    // Cirrus veils over clear ground and ochre streaks inside the cloud body.
    float veil;
    float streak;
};

// fadeFine: fade the finest octave by the pixel footprint. A bake keeps it and lets
// the cubemap mips do the fading instead.
TitanField TitanFieldAt(float3 spherePoint, bool fadeFine)
{
    TitanField field;
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
    float fineFade = 1.0;
    if (fadeFine)
    {
        float footprint = max(length(ddx(local)), length(ddy(local)));
        fineFade = 1.0 - smoothstep(0.035, 0.12, footprint);
    }
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

    // A higher, stretched layer crosses clear regions instead of tracing
    // their perimeter. Its own field produces interrupted cirrus filaments.
    float cirrus = Perlin(q * float3(3.2, 4.0, 3.2) + 63.8);

    field.density = density;
    // Dense interiors get billow relief too, while the mask is eroded mainly at margins.
    field.height = large * 0.52 + medium * 0.32 + small * 0.16;
    field.medium = medium;
    field.veil = cirrus + (medium - 0.5) * 0.14;
    field.streak = cirrus + (small - 0.5) * 0.2;
    return field;
}

// Height gradient becomes relief: its projection on the light. Gradient magnitude grows
// with noise frequency; normalize to the default scale of 3.
float3 ReliefVector(float3 gradient)
{
    return gradient * 0.24 / _PatternScale;
}

#endif
