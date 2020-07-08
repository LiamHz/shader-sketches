/*
This is my first 2D shader :)
I was inspired to remake Intrinsic Gravity
after seeing digital's version
https://www.shadertoy.com/view/XtcBzr
I used some of their ideas to start with
(using HSV, setting V to the SDF value)
but I've done my best to go farther
*/

#define PI 3.1415926535
vec3 hsv2rgb(vec3 c) {
    vec3 rgb = clamp(abs(mod(c.x*6.0+vec3(0.0,4.0,2.0),6.0)-3.0)-1.0, 0.0, 1.0);
    return c.z * mix( vec3(1.0), rgb, c.y);
}

float sdfCircle(vec2 p, float r) {
    float dist = 1.0 - (length(p) - r);
    dist = smoothstep(0.0, 0.1, dist);
    return dist;
}

#define SCENE_CIRCLES_START 0.0
#define tc1 (SCENE_CIRCLES_START + 2.0)
#define tc2 (tc1 + 1.4)
#define tc3 (tc2 + 1.4)
#define tc4 (tc3 + 1.4)
#define tc5 (tc4 + 0.8)
#define tc6 (tc5 + 0.4)
#define tc7 (tc6 + 1.0)
#define SCENE_CIRCLES_END tc7

vec3 sceneCircles(vec2 uv, float time) {
    vec3 color;
    float nCircles= 12.0;
    float camZoom = 0.05;
    
    // Continously rotate the scene
    float rotS = 0.1;
    float rot = time > tc1 ? (time - tc1)*rotS : 0.0;
    uv *= mat2(cos(rot), -sin(rot), sin(rot), cos(rot));

    float dist = smoothstep(0.0, tc1, time) * 2.97;
    
    float radius = 0.5 
        		   + smoothstep(tc2, tc2+0.2, time)*0.61
                   + smoothstep(tc3, tc3+0.2, time)*0.48
                   + smoothstep(tc4, tc4+0.2, time)*0.5
                   + smoothstep(tc5, tc5+1.6, time)*16.0;
    
    // Color intensity / transparency
    float alpha = 0.20;

    for (int i=0; i<int(nCircles); i++) {
      vec2 pos = uv/camZoom;
      float x = dist*cos(2.0*PI*float(i)/nCircles);
      float y = dist*sin(2.0*PI*float(i)/nCircles);
      pos.x += x;
      pos.y += y;

      // SDF controls color brightness
      // SDF of <= 0 indicates point is outside of shape
      float sdf = clamp(sdfCircle(pos, radius), 0.0, alpha);

      float hue = float(i)/nCircles;
      color += hsv2rgb(vec3(hue, 0.8, sdf));
    }

    return color;
}

#define SCENE_QUADS_START SCENE_CIRCLES_END
#define tq1 (SCENE_QUADS_START + 1.0)
#define tq2 (tq1 + 0.5)
#define tq3 (tq2 + 3.0)
#define tq4 (tq3 + 8.0)
#define SCENE_QUADS_END tq4

vec3 sceneQuads(vec2 uv, float time) {
    vec2 pos;
    vec3 color;
    float patternMask;
    
    if (time < tq2) {
        pos = uv;
        // min(smoothstep(..., time) , 0.5) transitions the "border" from the right edge to the center
        // step(-pos.x + 0.5...) starts the movement from the right edge (since pos goes from -1/2 to 1/2) 
        patternMask = pos.y > 0.0 ?     step(-pos.x+0.5, min(smoothstep(tq1, tq2, time), 0.5)) : 
                      pos.y < 0.0 ? 1.0-step(-pos.x+0.5, min(smoothstep(tq1, tq2, time), 0.5)) : 0.0;
    } else {
        // Rotation
        float rot = time > tq3 ? smoothstep(tq3, tq3+0.2, time)*PI/4.0 : 0.0;
    	uv *= mat2(cos(rot), -sin(rot), sin(rot), cos(rot));
        
        // Stop increasing size when rotation begins
        float size = 1.12*min(1.0+time-tq1, tq3-SCENE_QUADS_START);
        
        // Checkerboard pattern
        pos = floor(uv * size);
        patternMask = mod(pos.x + mod(pos.y, 2.0), 2.0);
        
        patternMask = 1.0-patternMask;
    }
    
    color = patternMask * vec3(1.0);

    return color;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    float time;
    time = mod(iTime,SCENE_QUADS_END+1.0);
    //time += SCENE_QUADS_START;
    // NDC from -1/2 to 1/2
    vec2 uv = (fragCoord - 0.5*iResolution.xy)/iResolution.x;
    
    vec3 outScene;
    if (time < SCENE_CIRCLES_END) {
        // Render scene
        vec3 scene1 = sceneCircles(uv, time);             // Black background
        outScene = mix(scene1, vec3(0), smoothstep(tc6, tc7, time));
        //vec3 scene2 = vec3(1.0) - sceneCircles(uv, time); // White background
        //vec3 outScene = mix(scene1, scene2, smoothstep(tc6, tc6+0.7, time));
    } else {
        vec3 scene1 = sceneQuads(uv, time);
    	outScene = scene1;
    }

    vec3 col = outScene;
    fragColor = vec4(col, 1.0);
}
