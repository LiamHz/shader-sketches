#version 150

uniform sampler2D prevPass;
uniform sampler2D prevDepthPass;

in VertexData {
    vec4 v_position;
    vec3 v_normal;
    vec2 v_texcoord;
} inData;

out vec4 fragColor;

vec4 getSurroundingTexels(sampler2D tex, vec2 texcoord, float sSize) {
    vec4 val;
    val += floor(texture(tex, texcoord +           vec2( 0, 1) *sSize));
    val += floor(texture(tex, texcoord +           vec2( 0,-1) *sSize));
    val += floor(texture(tex, texcoord +           vec2( 1, 0) *sSize));
    val += floor(texture(tex, texcoord +           vec2(-1, 0) *sSize));
    val += floor(texture(tex, texcoord + normalize(vec2( 1, 1))*sSize));
    val += floor(texture(tex, texcoord + normalize(vec2( 1,-1))*sSize));
    val += floor(texture(tex, texcoord + normalize(vec2(-1, 1))*sSize));
    val += floor(texture(tex, texcoord + normalize(vec2(-1,-1))*sSize));
    
    return val;
}

void main(void) {
    float outlineSize = 0.005;
    
    vec3 depthVal              = vec3(texture(prevDepthPass, inData.v_texcoord));
    float neighborDepthValsSum = getSurroundingTexels(prevDepthPass, inData.v_texcoord, outlineSize).r;
    
    bool isEdge = bool(depthVal.x == 1 && neighborDepthValsSum < 8);
    
    if (isEdge) {
        fragColor = vec4(1);
        return;
    }
 
    fragColor = texture(prevPass, inData.v_texcoord);
}
