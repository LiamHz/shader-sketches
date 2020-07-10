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

// step(pos.x + 0.5, time) screen wipe from left to right edge
// -pos.x flips the edge / wipe direction
// 1.0-step(...) flips the color
// min(smoothstep(...), EDGE) won't move past EDGE 
vec3 sceneIntro(vec2 pos, float time) {
    if (time < ti2) {
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
tq5: Start of sub squares growing
tq6: Start of flattening to stripes
*/

#define SCENE_QUADS_START SCENE_INTRO_END
#define tq1 (SCENE_QUADS_START + 3.0*beat)
#define tq2 (tq1 + halfbeat)
#define tq3 (tq2 + 1.5*beat)
#define tq4 (tq3 + beat)
#define tq5 (tq4 + 2.0*beat)
#define tq6 (tq5 + 2.0*beat)
#define SCENE_QUADS_END (tq6 + 2.0*beat)

float box(vec2 uv, vec2 size) {
    // Increase blur quadratically as size increases
    float blur = 0.00025*size.x*size.x;
    vec2 edge = vec2(0.5)-size*0.5;
    vec2 pos = smoothstep(edge, edge+blur,     uv)
     	       * smoothstep(edge, edge+blur, 1.0-uv);
    
    // Apply a NAND on the colors
    return 1.0 - pos.x * pos.y;
}

vec2 rotate2D(vec2 pos, float angle){
    pos -= 0.5;
    pos =  mat2(cos(angle),-sin(angle),
                sin(angle),cos(angle)) * pos;
    pos += 0.5;
    return pos;
}

// Side length of grid squares
// For a square that is rotated 45 degrees, and is contained
// by the unit square, its side length is sqrt(1/2)
#define z pow(0.5, 0.5)

vec2 tile(vec2 pos, float zoom){
    pos *= zoom;		    // Changes range from [0, 1] to [0, `zoom`]
    pos.x += 0.5;       // Shift tiling to the right by half a unit
    
    return fract(pos);	// Creates `zoom` tilings
}

vec3 sceneQuads(vec2 pos, float time) {
    vec3 color;
    float bColor;
    
    if (time < tq2) {
        bColor = pos.y > 0.0 ?     step(-pos.x+0.5, min(smoothstep(tq1, tq2, time), 0.5)) : 
                 pos.y < 0.0 ? 1.0-step(-pos.x+0.5, min(smoothstep(tq1, tq2, time), 0.5)) : 0.0;
    } else {
        // Decrease size
        float size = z + z*4.0*smoothstep(tq3, tq4, time)
            		   + z*2.0*smoothstep(tq5, tq5+halfbeat, time);
        
        // Global rotation
        float rot = smoothstep(tq4, tq4+halfbeat, time)*PI/4.0;
        rot -= PI/4.0;
        pos *= mat2(cos(rot), -sin(rot), sin(rot), cos(rot));
        
        // s > 1.0 will make grid 2 and 3 visible
        float s = 1.0+0.5*smoothstep(tq5, tq5+halfbeat, time);
        
        // Create 3 separate grids
        vec2 pos1 = pos;
        pos1 = tile(pos, size);
        pos1 = rotate2D(pos1, PI/4.0);
        float grid1 = box(pos1, vec2(z)/s);
        
        vec2 pos2 = pos;
        pos2.x -= 0.5/size; // Center square on edges of grid1
        pos2 = tile(pos2, size); 
        pos2 = rotate2D(pos2, PI/4.0);
        float grid2 = box(pos2, z-vec2(z)/s); 
        
        vec2 pos3 = pos;
        pos3.y -= 0.5/size; // Center square on edges of grid1
        pos3 = tile(pos3, size);
        pos3 = rotate2D(pos3, PI/4.0);
        float grid3 = box(pos3, z-vec2(z)/s); 
        
        bColor = min(grid1, min(grid2, grid3));
    }
    
    color = vec3(bColor);

    return color;
}

/*
tc1: circles start rotating / stop moving out
tc2: Radius increase 1
tc3: Radius increase 2
tc4: Radius increase 3
tc5: Radius increase 4
*/

#define SCENE_CIRCLES_START SCENE_QUADS_END
#define tc1 (SCENE_CIRCLES_START + 2.0*beat)
#define tc2 (tc1 + beat)
#define tc3 (tc2 + beat)
#define tc4 (tc3 + beat)
#define tc5 (tc4 + halfbeat)
#define SCENE_CIRCLES_END (tc5)

// I used digital's shader as a starter for this scene
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
    
    // Change circle radius at specific times
    float radius = 0.5 + smoothstep(tc2, tc2+0.2, time)*0.61
                       + smoothstep(tc3, tc3+0.2, time)*0.48
                       + smoothstep(tc4, tc4+0.2, time)*0.5
                       + smoothstep(tc5, tc5+1.6, time)*16.0;
    
    // Color intensity / transparency
    float alpha = 0.20;

    for (float i=0.0; i<nCircles; i++) {
        vec2 pos = uv/camZoom;

        // Space circles along radius of the origin
        float x = dist*cos(2.0*PI*i/nCircles);
        float y = dist*sin(2.0*PI*i/nCircles);
        pos.x += x;
        pos.y += y;

        // SDF controls color brightness
        // SDF of <= 0 indicates point is outside of shape
        float sdf = clamp(sdfCircle(pos, radius), 0.0, alpha);

        // Evenly space hue
        float hue = float(i)/nCircles;
        color += hsv2rgb(vec3(hue, 0.8, sdf));
    }

    return color;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord) {
    float time = iTime;
    
    time += tq5;                         // Start at specified scene
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
        scene = mix(sceneCircles(uv, time), vec3(0), 
                    smoothstep(SCENE_CIRCLES_END-halfbeat, SCENE_CIRCLES_END, time));
    } else {
        scene = vec3(0.8, 0.2, 0.2);   
    }

    fragColor = vec4(scene, 1.0);
}
