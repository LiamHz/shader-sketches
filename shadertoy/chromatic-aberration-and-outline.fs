# define EDGE_WIDTH 0.125
# define DISTORTION_DISTANCE 0.01

/* 
Chromatic Aberration (lines 199-221)
- Acheived by sending 3 rays into the scene,
- Each ray's x-direction is shifted by sin(iTime)
- Each ray is responsible for 1 of the fragment's RGB values

Object Outline (line 90)
- If a marched ray comes close to a SDF, but never intersects
  with a surface, that ray has intersected with a surface edge
*/

#define EPS 0.0001

struct Light {
    vec3 position;
    vec3 intensity;
};

// CSG operations
float opI(float d1, float d2) {
    return max(d1, d2);
}

float opS(float d1, float d2) {
    return max(d1, -d2);
}

// CSG union and material operator
// d1 is a vec2 where .x is the distance
// and .y is the color / material
vec2 opU(vec2 d1, vec2 d2) {
    return (d1.x < d2.x) ? d1 : d2;
}

// SDFs
float sdfSphere(vec3 p, float radius) {
    return length(p) - radius;
}

float sdfTorus(vec3 pos, vec2 t) {
    vec2 q = vec2(length(0.80*pos.xz + 1.75) - 2.75*t.x, 1.25*pos.y);
    return 1.1*length(q) - t.y;
}

float sdfCube(vec3 p) {
    vec3 d = abs(p) - vec3(1.0, 1.0, 1.0);
    float insideDistance = min(max(d.x, max(d.y, d.z)), 0.0);
    float outsideDistance = length(max(d, 0.0));
    return insideDistance + outsideDistance;
}

// Map of scene SDFs
vec2 map(vec3 p) {
    float cubeD   = sdfCube(p);
    float sphereD = sdfSphere((p) / 1.2, 1.0) * 1.2;
    float torusD  = sdfTorus(p, vec2(0.88));

    float cubeSphereD = opI(cubeD, sphereD);
    
    vec2 res = opU(vec2(cubeSphereD, 0.3),
                   vec2(torusD, 0.6));

    return res;
}

vec3 raymarch(vec3 viewPos, vec3 ray) {
    vec2 res;
    float depth = EPS;
    float minDist = 16.0;
    float maxDepth = 16.0;
    int maxIterations = 64;

    for (int i = 0; i < maxIterations; i++) {
        // res.x is distance from nearest surface
        // res.x < EPS indicates surface intersection
        // res.y is material code
        res = map(viewPos + depth * ray);
        minDist = min(res.x, minDist);
        if (res.x < EPS || depth > maxDepth) break;
        depth += res.x;
    }
    
    float material = depth > maxDepth ? -1.0 : res.y;
    float isEdge = minDist < EDGE_WIDTH && depth > maxDepth ? 1.0 : 0.0;

    return vec3(depth, material, isEdge);
}

// Estimate normal of surface at point p by sampling nearby points
vec3 estimateNormal(vec3 p) {
    return normalize(vec3(
        map(vec3(p.x + EPS, p.y, p.z)).x - map(vec3(p.x - EPS, p.y, p.z)).x,
        map(vec3(p.x, p.y + EPS, p.z)).x - map(vec3(p.x, p.y - EPS, p.z)).x,
        map(vec3(p.x, p.y, p.z + EPS)).x - map(vec3(p.x, p.y, p.z - EPS)).x
    ));
}

vec3 getLightContrib(vec3 p, vec3 eye, vec3 k_d, vec3 k_s, 
                    float shininess, Light light) {
    vec3 N = estimateNormal(p);             // Normal
    vec3 L = normalize(light.position - p); // Light vector
    vec3 V = normalize(eye - p);            // View vector
    vec3 R = normalize(reflect(-L, N));     // Reflect vector
    
    float diff = max(dot(L, N), 0.0);
    float spec = pow(max(dot(R, V), 0.0), shininess);

    vec3 diffuse  = k_d * diff;
    vec3 specular = k_s * spec;

    return light.intensity * (diffuse + specular);
}

vec3 getLighting(vec3 p, vec3 viewPos) {
    vec3 k_a = vec3(0.3);
    vec3 k_d = vec3(0.7);
    vec3 k_s = vec3(1.0);
    float shininess = 16.0;

    vec3 ambientLightIntensity = vec3(1.0);
    vec3 lighting;

    Light light1;
    light1.intensity = vec3(0.5);
    light1.position  = vec3(20.0, 3.0, 5.0);

    lighting += getLightContrib(p, viewPos, k_d, k_s, shininess, light1);
    lighting += k_a * ambientLightIntensity;

    return lighting;
}

// Create look at matrix
mat4 setCamera(vec3 eye, vec3 center, vec3 up) {
    vec3 f = normalize(center - eye);
    vec3 s = normalize(cross(f, up));
    vec3 u = cross(s, f);

    return mat4(vec4(s, 0), vec4(u, 0), vec4(-f, 0), vec4(0, 0, 0, 1));
}

// Cosine based palette
// From iq: shadertoy.com/view/ll2GD3
vec3 palette(float t) {
    // Palette parameters
    vec3 a = vec3(0.5);
    vec3 b = vec3(0.5);
    vec3 c = vec3(1.0);
    vec3 d = vec3(0.0, 0.10, 0.2);
    return a + b*cos(6.28318*(c*t+d));
}

vec3 getPixel(vec3 viewPos, vec3 worldRay) {
    vec3 edgeColor       = vec3(1.0);
    vec3 backgroundColor = vec3(0.0);

    // r.x == length of ray at intersection, 
    // r.y == material (-1.0 if no intersection)
    // r.z == isEdge
    vec3 r = raymarch(viewPos, worldRay);

    if (r.z == 1.0) return edgeColor;
    if (r.y == -1.0) return backgroundColor;
    
    // Point of intersection of view ray with surface
    vec3 p = viewPos + r.x * worldRay;
    vec3 lighting = getLighting(p, viewPos);

    return palette(r.y) * lighting * 2.0;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    // Normalize screen coordinates
    vec2 p = (-iResolution.xy + 2.0*fragCoord)/iResolution.y;

    // Camera
    vec3 pos    = vec3(5.0, 6.0, 5.0);
    vec3 center = vec3(0.0);
    vec3 up     = vec3(0.0, 1.0, 0.0);
    mat4 camera = setCamera(pos, center, up);

    // Chromatic aberration
    // dx is distance to distort by
    float dx = 0.0;
    dx = (1.0 + sin(iTime*6.0)) * 0.5;
    dx *= 1.0 + sin(iTime*16.0) * 0.5;
    dx *= 1.0 + sin(iTime*19.0) * 0.5;
    dx *= 1.0 + sin(iTime*27.0) * 0.5;
    dx = pow(dx, 3.0);

    dx *= DISTORTION_DISTANCE;
    
    // Transform rays by distortion amount (dx),
    // and convert them from view to world space 
    vec3 ray1 = (camera * normalize(vec4(p.x     , p.y, -2.0, 0.0))).xyz;
    vec3 ray2 = (camera * normalize(vec4(p.x - dx, p.y, -2.0, 0.0))).xyz;
    vec3 ray3 = (camera * normalize(vec4(p.x + dx, p.y, -2.0, 0.0))).xyz;
    
    vec3 col;
    col.r = getPixel(pos, ray2).r;
    col.g = getPixel(pos, ray1).g;
    col.b = getPixel(pos, ray3).b;

    col *= (1.0 - dx * 0.5);
    
    fragColor = vec4(col, 1.0);
}
