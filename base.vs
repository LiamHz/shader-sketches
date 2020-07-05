#version 150

uniform float u_time;
uniform mat4 u_mvp;
uniform mat4 u_view;
uniform mat4 u_projection;

in vec4 a_position;
in vec3 a_normal;
in vec2 a_texcoord;

out VertexData {
    vec3 f_position;
    vec4 v_position;
    vec3 v_normal;
    vec2 v_texcoord;
} outData;

mat4 rotationY(float angle) {
    return mat4( cos(angle),    0, sin(angle),  0,
                          0,  1.0,          0,  0,
                -sin(angle),    0, cos(angle),  0,
                          0,    0,          0,  1);
}

void main(void) {
    outData.v_position = a_position;
    outData.v_texcoord = a_texcoord;
    
    // Construct model matrix
    mat4 scaleMatrix = mat4(0.5);
    mat4 translationMatrix = mat4(1);
    mat4 rotationMatrix = rotationY(u_time/2);
    mat4 modelMatrix = translationMatrix * rotationMatrix * scaleMatrix;
    
    // Convert to world-space (uniform scaling)
    outData.v_normal = mat3(modelMatrix) * a_normal;
    outData.f_position = vec3(modelMatrix * vec4(a_position));
    
    gl_Position = u_projection * u_view * vec4(outData.f_position,  1.0);
}
