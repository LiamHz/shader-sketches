// This is my first 2D shader :)

#define EPS 0.0000001
#define PI 3.1415926535

#define BPM 85.0
#define beat (60.0/BPM)
#define halfbeat (60.0/BPM/2.0)

vec3 hsv2rgb(vec3 c) {
    vec3 rgb = clamp(abs(mod(c.x*6.0+vec3(0.0,4.0,2.0),6.0)-3.0)-1.0, 0.0, 1.0);
    return c.z * mix( vec3(1.0), rgb, c.y);
}

vec2 rotate2D(vec2 pos, float angle){
    pos -= 0.5;
    pos =  mat2(cos(angle),-sin(angle),
                sin(angle),cos(angle)) * pos;
    pos += 0.5;
    return pos;
}

vec2 tile(vec2 pos, float zoom){
    pos   *= zoom;		 // Changes range from [0, 1] to [0, `zoom`]
    pos.x += 0.5;      // Shift tiling to the right by half a unit
    
    return fract(pos); // Creates `zoom` tilings
}

float sdfCircle(vec2 p, float r) {
    float dist = 1.0 - (length(p) - r);
    dist = smoothstep(0.0, 0.1, dist);
    return dist;
}

// Side length of grid squares
// For a square that is rotated 45 degrees, and is contained
// by the unit square, its side length is sqrt(1/2)
#define z pow(0.5, 0.5)

float box(vec2 uv, vec2 size, float blur) {
    vec2 edge = vec2(0.5)-size*0.5;
    vec2 pos = smoothstep(edge, edge+blur,     uv)
     	       * smoothstep(edge, edge+blur, 1.0-uv);
    
    // Apply a NAND on the colors
    return 1.0 - pos.x * pos.y;
}

/*
ti1: Start of wipe to white
ti2: Start of wipe to black
ti3: Start of half wipe
*/

#define SCENE_INTRO_START 0.0
#define ti1 (SCENE_INTRO_START + beat)
#define ti2 (ti1 + 4.0*beat)
#define ti3 (ti2 + 4.0*beat)
#define SCENE_INTRO_END (ti3 + 3.0*beat)
// 12 beats total

// step(pos.y + 0.5, time) wipes screen from bottom to top edge
// -pos.y flips the edge / wipe direction
// -step(...) flips the color
vec3 sceneIntro(vec2 pos, float time) {
    float d = 0.5*halfbeat;
    float col = step(-pos.y + 0.5,     smoothstep(ti1  , ti1+halfbeat, time))
              - step( pos.x + 0.5,     smoothstep(ti2  , ti2+halfbeat, time))
              + step( pos.y + 0.5, 0.5*smoothstep(ti3-d, ti3+d       , time));
    
    return vec3(col);
}

/*
tq1: Length of initial stillness
tq2: Length of horizontal slide
tq3: Start of zoom out
tq4: Start of global rotation 1
tq5: Start of sub squares growing
tq6: Start of flattening to stripes
tq7: Start of stripes sliding
tq8: Start of grid reformation stage 1
tq9: Start of grid reformation stage 2
tq10: Start of screen flash + grid shift + tile spin
tq11: Start of wipe from outside in
tq12: Start of global rotation 2
tq13: Start of global rotation 3
tq14: Start of tiles shrinking to nothing
*/


#define SCENE_QUADS_START SCENE_INTRO_END
#define tq1 (SCENE_QUADS_START + beat)
#define tq2 (tq1 + halfbeat)
#define tq3 (tq2 + 1.5*beat)
#define tq4 (tq3 + 1.5*beat)
#define tq5 (tq4 + 4.0*beat)
#define tq6 (tq5 + 4.5*beat)
#define tq7 (tq6 + 0.5*halfbeat)
#define tq8 (tq7 + 0.5*halfbeat+3.5*beat)
#define tq9 (tq8 + halfbeat)
#define tq10 (tq9 + 2.0*beat)
#define tq11 (tq10 + beat)
#define tq12 (tq11 + halfbeat)
#define tq13 (tq12 + halfbeat)
#define tq14 (tq13 + halfbeat)
#define SCENE_QUADS_END (tq14 + 2.0*beat)
// 24 beats total

vec3 sceneQuads(vec2 pos, float time) {
    vec3 color;
    float bColor;
    
    if (time < tq2) {
        float shift = smoothstep(tq1, tq2, time);
        bColor = pos.y > 0.0 ?     step(-pos.x+0.5, 0.5*shift) : 
                 pos.y < 0.0 ? 1.0-step(-pos.x+0.5, 0.5*shift) : 0.0;
    
    	return vec3(bColor);
    }       
    // Decrease size
    float size = z + z*4.0*smoothstep(tq3, tq4, time)
                   + z*2.0*smoothstep(tq5, tq5+halfbeat, time)
                   + z*0.25*smoothstep(tq5+halfbeat, tq6, time);

    // Global rotation
    float rot = -PI/4.0 + PI/4.0*smoothstep(tq4,  tq4 +    halfbeat, time)
        			          + PI/4.0*smoothstep(tq12, tq12+0.5*halfbeat, time)
        			          + PI/4.0*smoothstep(tq13, tq13+0.5*halfbeat, time);
    pos *= mat2(cos(rot), -sin(rot), sin(rot), cos(rot));

    // s > 1.0 will make grid 2 and 3 visible
    float s = 1.0+0.5*smoothstep(tq5, tq5+halfbeat, time);

    // Grid skewing
    float diffSkew   = 0.03;
    float centerSkew = 0.25;
    float skew = (centerSkew + diffSkew)
               * smoothstep(tq6, tq7, time);
    float miniSkew = max(0.0, skew - centerSkew);
    float subSkew  = miniSkew-min(skew, centerSkew - diffSkew);

    float reskew = (centerSkew + diffSkew) 
        	       * smoothstep(tq8, tq8+0.5*halfbeat, time);

    float unskew = pow(2.0, 0.5) * diffSkew
                 * smoothstep(tq9, tq9+0.5*halfbeat, time);

    vec2 mainGridSize = vec2( reskew, skew)-unskew  +vec2(z)/s;
    vec2 subGridSize  = vec2(-reskew, subSkew)    +z-vec2(z)/s;

    mainGridSize += z*smoothstep(tq14, tq14+0.5*halfbeat, time);

    // Wipe transition
    float wipeSize = (z+0.5)*(1.0 - smoothstep(tq11, tq11+halfbeat, time));

    // Only apply blur after tq4 (rotation)
    // and before tq6 (flattening to stripes)
    float blur = 0.005*step(tq4, time) * (1.0 - step(tq6, time));
    
    // Slide grids
    float slide = (1.0/size) * 0.1*smoothstep(tq7, tq8, time);
    pos += slide;

    // Screen flash and recenter grid
    pos = time >= tq10 ? pos - slide : pos;   
    if (time >= tq10 && time <= tq10+0.1*beat) return vec3(0.0);
    
    // Tile spin
    float spin = PI/2.0*smoothstep(tq10, tq10 + halfbeat, time);
    
    // Wipe square
    vec2 pos4 = pos;
    pos4 = rotate2D(pos4, PI/4.0);
    pos4.x += z;
    float wipeSquare = box(pos4, vec2(wipeSize), EPS);

    // Create 3 separate grids 
    vec2 pos1 = pos;
    pos1 = tile(pos, size);
    pos1 = rotate2D(pos1, PI/4.0+spin);
    float grid1 = box(pos1, mainGridSize, blur);

    // Center squares on edges of grid1
    vec2 shift = vec2(0.5/size, 0.0);

    vec2 pos2 = tile(pos - shift, size);
    pos2 = rotate2D(pos2, PI/4.0);
    float grid2 = box(pos2, subGridSize, 0.5*blur);

    vec2 pos3 = tile(pos - shift.yx, size);
    pos3 = rotate2D(pos3, PI/4.0);
    float grid3 = box(pos3, subGridSize, 0.5*blur);

    float grids = min(grid1, min(grid2, grid3));
    
    // Apply an XOR between the grids and wipeSquare
    bColor = mod(grids + wipeSquare, 2.0);

    return vec3(bColor);
}

/*
tc0: Circles start moving out
tc1: Circles start rotating
tc2: Radius increase 1
tc3: Radius increase 2
tc4: Radius increase 3
tc5: Radius increase 4
*/

#define SCENE_CIRCLES_START SCENE_QUADS_END
#define tc0 (SCENE_CIRCLES_START + 3.0*beat)
#define tc1 (tc0 + 3.0*beat)
#define tc2 (tc1 + beat)
#define tc3 (tc2 + beat)
#define tc4 (tc3 + beat)
#define tc5 (tc4 + halfbeat)
#define SCENE_CIRCLES_END (tc5 + 2.5*beat)
// 12 beats total

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

    float dist = smoothstep(tc0, tc1, time) * 2.97;
    
    // Change circle radius at specific times
    float radius = -1.0 +  1.5*smoothstep(SCENE_CIRCLES_START, tc0, time)
                        + 0.61*smoothstep(tc2, tc2+halfbeat, time)
                        + 0.48*smoothstep(tc3, tc3+halfbeat, time)
                        +  0.5*smoothstep(tc4, tc4+halfbeat, time)
                        + 16.0*smoothstep(tc5, tc5+beat, time);
    
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
    // Correct for the delay between Soundcloud 
    // and YouTube audio of Infinite Gravity
    float time = iTime + 0.4;
    
    float end = SCENE_CIRCLES_END; // End of last scene
    time += 0.0;                   // Start at specified scene
    time = mod(time, end);         // Loop at end of last scene
    
    // NDC from -1/2 to 1/2
    vec2 uv = (fragCoord - 0.5*iResolution.xy)/iResolution.x;
    
    vec3 scene;
    if (time < SCENE_INTRO_END) {
        scene = sceneIntro(uv, time); 
    } else if (time < SCENE_QUADS_END) {
        scene = sceneQuads(uv, time);
    } else if (time < SCENE_CIRCLES_END) {
        scene = mix(1.0-sceneCircles(uv, time), vec3(0), 
                    smoothstep(end-halfbeat, end, time));
    } else {
        scene = vec3(1.0);   
    }

    fragColor = vec4(scene, 1.0);
}
