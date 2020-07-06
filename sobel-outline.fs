#version 150

uniform sampler2D prevPass;
uniform sampler2D prevDepthPass;

in VertexData {
    vec4 v_position;
    vec3 v_normal;
    vec2 v_texcoord;
} inData;

out vec4 fragColor;

void makeKernel(inout vec4 n[9], sampler2D tex, vec2 coord, float stepSize) {
    float x = stepSize;

    n[0] = texture(tex, coord + vec2( -x, -x));
    n[1] = texture(tex, coord + vec2(0.0, -x));
    n[2] = texture(tex, coord + vec2(  x, -x));
    n[3] = texture(tex, coord + vec2( -x, 0.0));
    n[4] = texture(tex, coord);
    n[5] = texture(tex, coord + vec2(  x, 0.0));
    n[6] = texture(tex, coord + vec2( -x, x));
    n[7] = texture(tex, coord + vec2(0.0, x));
    n[8] = texture(tex, coord + vec2(  x, x));
}

void main(void) {
    float outlineSize = 0.002;
    vec3 currentColor = vec3(texture(prevPass, inData.v_texcoord));

    vec4 n[9];
    makeKernel(n, prevPass, inData.v_texcoord, outlineSize);
    vec4 sobelEdgeH = n[2] + (2.0*n[5]) + n[8] - (n[0] + (2.0*n[3]) + n[6]);
    vec4 sobelEdgeV = n[0] + (2.0*n[1]) + n[2] - (n[6] + (2.0*n[7]) + n[8]);
    vec4 sobel = sqrt((sobelEdgeH * sobelEdgeH) + (sobelEdgeV * sobelEdgeV));

    bool isEdge = bool(length(sobel.b) > 1.);
    
    if (isEdge) {
        fragColor = vec4(1);
        return;
    }
 
    fragColor = texture(prevPass, inData.v_texcoord);
}
