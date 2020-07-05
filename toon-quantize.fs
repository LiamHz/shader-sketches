#version 150

uniform sampler2D prevFrame;
uniform sampler2D prevPass;

in VertexData {
    vec4 v_position;
    vec3 v_normal;
    vec2 v_texcoord;
} inData;

out vec4 fragColor;

void main(void) {
    // Quantize color
    float quantize = 10;
    vec3 prevColor = texture(prevPass, inData.v_texcoord).rgb;
    prevColor *= quantize;
    prevColor += vec3(0.5);                                     // Round
    ivec3 intPrevColor = ivec3(prevColor);                      // Truncate
    vec3 quantizedPrevColor = vec3(intPrevColor) / quantize;    // Quantize
    
    fragColor = vec4(quantizedPrevColor, 1);
}
