#ifndef SUTURN_NOISE_INCLUDED
#define SUTURN_NOISE_INCLUDED

float3 Hash33(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yxx) * p.zyx);
}

// Gradient Perlin noise, evaluated directly in 3D: no texture or UV seam.
float Perlin(float3 p)
{
    float3 c = floor(p), f = frac(p);
    float3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float result = 0;
    [unroll] for (int z = 0; z <= 1; z++)
    [unroll] for (int y = 0; y <= 1; y++)
    [unroll] for (int x = 0; x <= 1; x++)
    {
        float3 o = float3(x, y, z);
        float3 g = normalize(Hash33(c + o) * 2.0 - 1.0 + 0.001);
        float3 w = lerp(1.0 - u, u, o);
        result += dot(g, f - o) * w.x * w.y * w.z;
    }
    return 0.5 + result * 0.9;
}

#endif
