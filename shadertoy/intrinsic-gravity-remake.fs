// This is my first 2D shader :)

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

#define BPM 85.0
#define beat (60.0/BPM)
#define halfbeat (60.0/BPM/2.0)

/*
ti1: Start of wipe to white
ti2: Start of wipe to black
ti3: Start of half wipe
*/

#define SCENE_INTRO_START 0.0
#define ti1 (SCENE_INTRO_START + halfbeat)
#define ti2 (ti1 + 4.0*beat)
#define ti3 (ti2 + 4.0*beat)
#define SCENE_INTRO_END (ti3+beat)

vec3 sceneIntro(vec2 pos, float time) {
    if (time < ti2) {
        // step(pos.y + 0.5...) starts the movement from the top edge
        return vec3(step(-pos.y + 0.5, smoothstep(ti1, ti1+halfbeat, time)));
    } else if (time < ti3) {
        return vec3(1.0-step(pos.x + 0.5, smoothstep(ti2, ti2+halfbeat, time)));  
    } else {
        return vec3(step(pos.y + 0.5, min(smoothstep(ti3, ti3+beat, time), 0.5)));
    }
}

/*
tq1: Initial stillness
tq2: Length of horizontal slide
tq3: Start of zoom out
tq4: Start of rotation
*/

#define SCENE_QUADS_START SCENE_INTRO_END
#define tq1 (SCENE_QUADS_START + 3.0*beat)
#define tq2 (tq1 + halfbeat)
#define tq3 (tq2 + 1.5*beat)
#define tq4 (tq3 + beat)
#define SCENE_QUADS_END (tq4 + beat)

vec3 sceneQuads(vec2 uv, float time) {
    vec2 pos;
    vec3 color;
    float patternMask;
    
    if (time < tq2) {
        pos = uv;
        // min(smoothstep(..., time), 0.5) transitions the "border" from the right edge to the center
        // step(-pos.x + 0.5...) starts the movement from the right edge (since pos goes from -1/2 to 1/2) 
        // 1.0-step(...) flips the color
        patternMask = pos.y > 0.0 ?     step(-pos.x+0.5, min(smoothstep(tq1, tq2, time), 0.5)) : 
                      pos.y < 0.0 ? 1.0-step(-pos.x+0.5, min(smoothstep(tq1, tq2, time), 0.5)) : 0.0;
    } else {
        // Rotation
        float rot = time > tq4 ? smoothstep(tq4, tq4+halfbeat, time)*PI/4.0 : 0.0;
        uv *= mat2(cos(rot), -sin(rot), sin(rot), cos(rot));
        
        // Stop increasing size when rotation begins
        float size = 1.0+4.0*smoothstep(tq3, tq4, time);
        
        // Checkerboard pattern
        pos = floor(uv * size);
        patternMask = mod(pos.x + mod(pos.y, 2.0), 2.0);
        
        patternMask = 1.0-patternMask;
    }
    
    color = patternMask * vec3(1.0);

    return color;
}

/*
tc1: circles start rotating / stop moving out
*/

#define SCENE_CIRCLES_START SCENE_QUADS_END
#define tc1 (SCENE_CIRCLES_START + 2.0*beat)
#define tc2 (tc1 + beat)
#define tc3 (tc2 + beat)
#define tc4 (tc3 + beat)
#define tc5 (tc4 + halfbeat)
#define tc6 (tc5 + 0.25*beat)
#define SCENE_CIRCLES_END (tc6 + beat)

// I used digital's shader  as a starter for this scene
// https://www.shadertoy.com/view/XtcBzr
vec3 sceneCircles(vec2 uv, float time) {
    vec3 color;
    float nCircles = 12.0;
    float camZoom  = 0.05;
    
    // Continously rotate the scene
    float rotS = 0.1;
    float rot = time > tc1 ? (time - tc1)*rotS : 0.0;
    uv *= mat2(cos(rot), -sin(rot), sin(rot), cos(rot));

    float dist = smoothstep(SCENE_CIRCLES_START, tc1, time) * 2.97;
    
    float radius = 0.5 + smoothstep(tc2, tc2+0.2, time)*0.61
                       + smoothstep(tc3, tc3+0.2, time)*0.48
                       + smoothstep(tc4, tc4+0.2, time)*0.5
                       + smoothstep(tc5, tc5+1.6, time)*16.0;
    
    // Color intensity / transparency
    float alpha = 0.20;

    for (float i=0.0; i<nCircles; i++) {
        vec2 pos = uv/camZoom;
        float x = dist*cos(2.0*PI*i/nCircles);
        float y = dist*sin(2.0*PI*i/nCircles);
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

void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    float time = iTime;
    
    
    time += SCENE_INTRO_START;           // Start at specified scene
    time = mod(time, SCENE_CIRCLES_END); // Loop at end of scene
    
    // NDC from -1/2 to 1/2
    vec2 uv = (fragCoord - 0.5*iResolution.xy)/iResolution.x;
    
    vec3 scene;
    if (time < SCENE_INTRO_END) {
        scene = sceneIntro(uv, time); 
    } else if (time < SCENE_QUADS_END) {
        scene = sceneQuads(uv, time);
    } else if (time < SCENE_CIRCLES_END) {
        // Fade to black at end of scene
        scene = mix(sceneCircles(uv, time) vec3(0), smoothstep(tc6, SCENE_CIRCLES_END, time));
    } else {
        outScene = vec3(0.8, 0.2, 0.2);   
    }

    vec3 col = scene;
    fragColor = vec4(col, 1.0);
}
