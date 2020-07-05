#version 150

uniform mat4 u_view;
uniform float u_time;

in VertexData {
    vec3 f_position;
    vec4 v_position;
    vec3 v_normal;
    vec2 v_texcoord;
} inData;

out vec4 fragColor;

struct Light {
    vec3 position;
    vec3 intensity;
};

vec3 getLightContrib(vec3 p, vec3 normal, vec3 eye,
                     vec3 k_d, vec3 k_s, float shininess, Light light) {
    vec3 N = normal;                        // Normal
    vec3 L = normalize(light.position - p); // Light vector
    vec3 V = normalize(eye - p);            // View vector
    vec3 R = normalize(reflect(-L, N));     // Reflect vector
    
    float diff = max(dot(L, N), 0.0);
    float spec = pow(max(dot(R, V), 0.0), shininess);

    vec3 diffuse  = k_d * diff;
    vec3 specular = k_s * spec;

    return light.intensity * (diffuse + specular);
}

vec3 getLighting(vec3 p, vec3 normal, vec3 eye) {
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

    lighting += getLightContrib(p, normal, eye, k_d, k_s, shininess, light1);
    lighting += getLightContrib(p, normal, eye, k_d, k_s, shininess, light2);

    return lighting;
}

vec3 sampleTexture(sampler2D tex) {
    return texture(tex, vec2(inData.v_texcoord.x, 1-inData.v_texcoord.y)).rgb;
}

void main(void) {
    // Last column of view matrix is the view position
    vec3 viewPos = vec3(u_view[3].xyz);
    vec3 light = getLighting(inData.f_position, inData.v_normal, viewPos);

    vec3 outColor = vec3(1, 0, 0) * light;
    
    fragColor = vec4(outColor, 1);
}
