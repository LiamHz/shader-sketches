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

const float PI = 3.1415926535;

struct Light {
    vec3 position;
    vec3 intensity;
};

// Affine transforms
mat4 rotationX(float angle) {
    float c = cos(angle);
    float s = sin(angle);
    return mat4( 1, 0, 0, 0,
                 0, c,-s, 0,
                 0, s, c, 0,
                 0, 0, 0, 1);
}

mat4 rotationZ(float angle) {
    float c = cos(angle);
    float s = sin(angle);
    return mat4( c,-s, 0, 0,
                 s, c, 0, 0,
                 0, 0, 1, 0,
                 0, 0, 0, 1);
}


// Constructive Solid Geometry (CSG) operations
float intersectSDF(float distA, float distB) {
    return max(distA, distB);
}

float unionSDF(float distA, float distB) {
    return min(distA, distB);
}

float differenceSDF(float distA, float distB) {
    return max(distA, -distB);
}

// SDF for a cube centered at the origin with width = height = length = 2.0
float cubeSDF(vec3 p) {
    // If d.x < 0, then -1 < p.x < 1, and same logic applies to p.y, p.z
    // So if all components of d are negative, then p is inside the unit cube
    vec3 d = abs(p) - vec3(1.0, 1.0, 1.0);
    
    // Assuming p is inside the cube, how far is it from the surface?
    // Result will be negative or zero.
    float insideDistance = min(max(d.x, max(d.y, d.z)), 0.0);
    
    // Assuming p is outside the cube, how far is it from the surface?
    // Result will be positive or zero.
    float outsideDistance = length(max(d, 0.0));
    
    return insideDistance + outsideDistance;
}

// SDF for a sphere centered at the origin
float sphereSDF(vec3 p, float radius) {
    return length(p) - radius;
}

// SDF for a cylinder centered at the origin, 
// a and b are endpoints of the cylinder
float cylinderSDF(vec3 p, vec3 a, vec3 b, float r) {
    vec3  ba = b - a;
    vec3  pa = p - a;
    float baba = dot(ba,ba);
    float paba = dot(pa,ba);
    float x = length(pa*baba-ba*paba) - r*baba;
    float y = abs(paba-baba*0.5)-baba*0.5;
    float x2 = x*x;
    float y2 = y*y*baba;
    float d = (max(x,y)<0.0)?-min(x2,y2):(((x>0.0)?x2:0.0)+((y>0.0)?y2:0.0));
    return sign(d)*sqrt(abs(d))/baba;
}

// Absolute of the return value is distance to surface
// Sign indicates if point is inside (-) or outside (+) surface
float sceneSDF(vec3 p) {
    // Note: Transformations can be applied by using the inverse 
    //       of the desired transformation matrix
    //       EX: p + vec3(-1, 0, 0) will move the SDF 1 unit to the right
    
    // Note: Transformations must preserve the distances between points
    //       EX: sdf(p / scalingFactor) --> will result in distorted distances
    //           sdf(p / scalingFactor) * scalingFactor --> won't distort
    
    // Note: Understimating the distance is better than overestimating
    //       For non-uniform scaling multiply by the minimum of the components
    //       EX: sdf(p / vec3(x, y, z)) * min(x, min(y, z))
    
    float cubeD   = cubeSDF(p);
    float sphereD = sphereSDF((p) / 1.2, 1.0) * 1.2;
    
    float cS = 1.2;   // Cylinder scaling factor
    float cR = 0.6 + sin(u_time) * 0.4;
    float xCylD = cylinderSDF(p, vec3(-1,  0,  0)*cS, vec3(1, 0, 0)*cS, cR);
    float yCylD = cylinderSDF(p, vec3( 0, -1,  0)*cS, vec3(0, 1, 0)*cS, cR);
    float zCylD = cylinderSDF(p, vec3( 0,  0, -1)*cS, vec3(0, 0, 1)*cS, cR);
    
    float sR = 0.2;                 // Sphere radius
    float sT = sin(u_time) * 2.0;   // Transform scaler
    float sUp       = sphereSDF(p - vec3( 0, 1, 0)*sT, sR);
    float sDown     = sphereSDF(p - vec3( 0,-1, 0)*sT, sR);
    float sLeft     = sphereSDF(p - vec3(-1, 0, 0)*sT, sR);
    float sRight    = sphereSDF(p - vec3( 1, 0, 0)*sT, sR);
    float sForward  = sphereSDF(p - vec3( 0, 0, 1)*sT, sR);
    float sBack     = sphereSDF(p - vec3( 0, 0,-1)*sT, sR);

    float uniCyls          = unionSDF(xCylD, unionSDF(yCylD, zCylD));
    float interCircCube    = intersectSDF(cubeD, sphereD);
    float diffCircCubeCyls = differenceSDF(interCircCube, uniCyls);
    float sixSpheres       = unionSDF(unionSDF(unionSDF(sUp, sDown),
                                      unionSDF(sLeft, sRight)),
                                      unionSDF(sForward, sBack));

    float combo = unionSDF(diffCircCubeCyls, sixSpheres);

    return combo;
}

// Return shortest distance from the eye to the scene surface along ray 
float shortestDistanceToSurface(vec3 eye, vec3 marchingDirection,
                                float initialDepth, float maxDepth) {
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
    // Use surface normal as ambient color
    vec3 N = estimateNormal(p);
    vec3 normalAmbient = (N + vec3(1)) / 2.0;
    
    vec3 k_a = vec3(0.3);
    vec3 k_d = normalAmbient;
    vec3 k_s = vec3(1.0);
    float shininess = 8.0;


    vec3 ambientLight = vec3(0.2);
    vec3 lighting = ambientLight * k_a * normalAmbient;

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

// center: Center of scene
mat4 getLookAtMatrix(vec3 eye, vec3 center, vec3 up) {
    vec3 f = normalize(center - eye);
    vec3 s = normalize(cross(f, up));
    vec3 u = cross(s, f);

    return mat4(vec4(s, 0), vec4(u, 0), vec4(-f, 0), vec4(0, 0, 0, 1));
}

void main() {
    vec2 resolution = u_resolution;
    
    vec3 eye = vec3(sin(u_time) * 5.0, 4.0, cos(u_time) * 5.0);
    vec3 viewDir = getRayDirection(45.0, resolution, gl_FragCoord.xy);

    // Transform from view to world space
    mat4 viewToWorld = getLookAtMatrix(eye, vec3(0), vec3(0, 1, 0));
    vec3 worldDir    = (viewToWorld * vec4(viewDir, 0.0)).xyz;

    float dist = shortestDistanceToSurface(eye, worldDir, MIN_DIST, MAX_DIST);
    
    // Didn't hit anything
    if (dist > MAX_DIST - EPSILON) {
        fragColor = vec4(0);
        return;
    }
    
    // Point of intersection of view ray with surface
    vec3 p = eye + dist * worldDir;

    vec3 lighting = getLighting(p, eye);
    vec3 outColor = lighting;

    fragColor = vec4(outColor, 1);
}
