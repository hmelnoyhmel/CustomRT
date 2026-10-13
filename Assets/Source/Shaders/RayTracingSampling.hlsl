#ifndef RAY_TRACING_SAMPLING_INCLUDED
#define RAY_TRACING_SAMPLING_INCLUDED

// PCG (permuted congruential generator)
uint NextRandom(inout uint state)
{
    state = state * 747796405 + 2891336453;
    uint result = ((state >> ((state >> 28) + 4)) ^ state) * 277803737;
    result = (result >> 22) ^ result;
    return result;
}

float RandomValue(inout uint state)
{
    return NextRandom(state) / 4294967295.0; // 2^32 - 1
}

// Calculate a random direction
float3 RandomDirection(inout uint state)
{
    float u0 = RandomValue(state);
    float u1 = RandomValue(state);
    
    float z = 1 - 2 * u0;
    float phi = 2 * PI * u1;
    float r = sqrt(max(0, 1 - z * z));
    return float3(r * cos(phi), r * sin(phi), z);
}

float3 RandomHemisphereDirection(float3 normal, inout uint rngState)
{
    float3 dir = RandomDirection(rngState);
    return dir * sign(dot(normal, dir));
}

#endif