#version 150

uniform float u_time;
uniform vec2 u_resolution;

in VertexData {
    vec4 v_position;
    vec3 v_normal;
    vec2 v_texcoord;
} inData;

out vec4 fragColor;

// Ray marching parameters
const int MAX_MARCHING_STEPS = 255;
const float MIN_DIST = 0.0;
const float MAX_DIST = 100.0;
const float EPSILON = 0.0001;

struct Light {
    vec3 position;
    vec3 intensity;
};

// SDF for a sphere centered at the origin with radius 1
float sphereSDF(vec3 p) {
    return length(p) - 1.0;
}

// Absolute of the return value is distance to surface
// Sign indicates if point is inside (-) or outside (+) surface
float sceneSDF(vec3 p) {
    return sphereSDF(p);
}

// Return shortest distance from the eye to the scene surface along marching direction
float shortestDistanceToSurface(vec3 eye, vec3 marchingDirection, float initialDepth, float maxDepth) {
    float depth = initialDepth;
    for (int i = 0; i < MAX_MARCHING_STEPS; i++) {
        // dist is distance from surface
        // Negative dist indicates surface intersection
        float dist = sceneSDF(eye + depth * marchingDirection);
        if (dist < EPSILON) {
            return depth;
        }
        depth += dist;
        if (depth >= maxDepth) {
            return maxDepth;
        }
    }
    return maxDepth;
}
            

// Return normalized direction to march in from the eye point for a single pixel
vec3 getRayDirection(float fov, vec2 resolution, vec2 fragCoord) {
    // Move origin from bottom left to center of screen
    vec2 xy = fragCoord - resolution / 2.0;
    
    // Get the z-distance from pixel given resolution and vertical FoV
    // Diagram shows that: tan(radians(fov)/2) == (resolution.y * 0.5) / z
    // Diagram: https://stackoverflow.com/a/10018680
    // Isolating for z gives
    float z = (resolution.y * 0.5) / tan(radians(fov) / 2.0);
    return normalize(vec3(xy, -z));
}

// Estimate normal of surface at point p by sampling nearby points
vec3 estimateNormal(vec3 p) {
    return normalize(vec3(
        sceneSDF(vec3(p.x + EPSILON, p.y, p.z)) - sceneSDF(vec3(p.x - EPSILON, p.y, p.z)),
        sceneSDF(vec3(p.x, p.y + EPSILON, p.z)) - sceneSDF(vec3(p.x, p.y - EPSILON, p.z)),
        sceneSDF(vec3(p.x, p.y, p.z + EPSILON)) - sceneSDF(vec3(p.x, p.y, p.z - EPSILON))
    ));
}

vec3 getLightContrib(vec3 p, vec3 eye, vec3 k_d, vec3 k_s, float shininess, Light light) {
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

vec3 getLighting(vec3 p, vec3 eye) {
    vec3 k_a = vec3(0.3);
    vec3 k_d = vec3(0.7);
    vec3 k_s = vec3(1.0);
    float shininess = 8.0;

    vec3 ambientLight = vec3(0.2);
    vec3 lighting = ambientLight * k_a;

    Light light1;
    light1.intensity = vec3(0.4);
    light1.position  = vec3(4.0 * sin(u_time), 2.0, 4.0 * cos(u_time));

    Light light2;
    light2.intensity = vec3(0.4);
    light2.position  = vec3(2.0 * sin(0.4 * u_time), 2.0 * cos(0.4 * u_time), 2.0);

    lighting += getLightContrib(p, eye, k_d, k_s, shininess, light1);
    lighting += getLightContrib(p, eye, k_d, k_s, shininess, light2);

    return lighting;
}

void main() {
    vec2 resolution = u_resolution;
    
    vec3 eye = vec3(0.0, 0.0, 5.0);
    vec3 dir = getRayDirection(45.0, resolution, gl_FragCoord.xy);
    float dist = shortestDistanceToSurface(eye, dir, MIN_DIST, MAX_DIST);
    
    // Didn't hit anything
    if (dist > MAX_DIST - EPSILON) {
        fragColor = vec4(0.0, 0.0, 0.0, 0.0);
        return;
    }
    
    // Point of intersection of view ray with surface
    vec3 p = eye + dist * dir;

    vec3 lighting = getLighting(p, eye);
    vec3 outColor = vec3(1, 0, 0) * lighting;

    fragColor = vec4(outColor, 1);
}
