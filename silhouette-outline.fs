#version 150

uniform sampler2D prevPass;
uniform sampler2D prevDepthPass;

in VertexData {
    vec4 v_position;
    vec3 v_normal;
    vec2 v_texcoord;
} inData;

out vec4 fragColor;

float getSumOfSurroundingDepthValues(sampler2D tex, vec2 texcoord) {
    float surroundingDepthValues;
    float stepSize = 0.005;

    for (int i=0; i<9; i++) {
        int rowN = i/3;
        int colN = int(mod(i, 3));

        float modY = 1 - rowN;
        float modX = colN - 1;

        vec2 modifier = vec2(modX, modY);

        // Don't normalzie the zero vector
        if (length(modifier) != 0) {
          modifier = normalize(modifier);
        }
        modifier *= stepSize;
        
        vec4 depthVal = texture(tex, texcoord + modifier);
        surroundingDepthValues += floor(depthVal.r);
    }

    return surroundingDepthValues;
}

void main(void) {
    vec3 depthVal = vec3(texture(prevDepthPass, inData.v_texcoord));
    float sumDepthVals = getSumOfSurroundingDepthValues(prevDepthPass, inData.v_texcoord);
    
    bool isEdge = bool(depthVal.x == 1 && sumDepthVals < 9);
    
    if (isEdge) {
        fragColor = vec4(1);
    } else {
        fragColor = texture(prevPass, inData.v_texcoord);
    }
}
