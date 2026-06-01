from PIL import Image, ImageDraw, ImageFilter
import math, os

SIZE = 512
MED  = 400
MO   = (SIZE - MED) // 2   # 56 — medallion offset
BLK  = (22, 22, 22, 255)
OL_W = 4                   # outline width

# ─── shared helpers ──────────────────────────────────────────────────────────

def make_base():
    img = Image.new("RGBA", (SIZE, SIZE), (255, 255, 255, 255))
    sh  = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    shd = ImageDraw.Draw(sh)
    shd.rounded_rectangle([MO+5, MO+5, MO+MED+5, MO+MED+5], radius=52, fill=(0,0,0,70))
    sh  = sh.filter(ImageFilter.GaussianBlur(13))
    img = Image.alpha_composite(img, sh)
    d   = ImageDraw.Draw(img)
    d.rounded_rectangle([MO, MO, MO+MED, MO+MED], radius=52, fill=(186,186,186,255))
    return img

def poly(d, pts, fill):
    d.polygon(pts, fill=fill)
    n = len(pts)
    for i in range(n):
        d.line([pts[i], pts[(i+1)%n]], fill=BLK, width=OL_W)

def ellipse(d, box, fill, ow=OL_W):
    d.ellipse(box, fill=fill)
    d.ellipse(box, outline=BLK, width=ow)

def rect_pts(x, y, w, h):
    return [(x,y),(x+w,y),(x+w,y+h),(x,y+h)]

# ─── LOSS PREVENTION ─────────────────────────────────────────────────────────

def loss_prevention():
    img = make_base()
    d   = ImageDraw.Draw(img)

    # Anchor: camera housing centre at ~(265, 255)
    cx, cy = 265, 255

    # ── Wall plate ──────────────────────────────────────────────────────────
    wp = [(cx-130, cy-115),(cx-95, cy-115),(cx-95, cy+70),(cx-130, cy+70)]
    poly(d, wp, (125,125,130,255))
    # wall-plate top bevel
    wpt = [(cx-130,cy-115),(cx-95,cy-115),(cx-90,cy-130),(cx-135,cy-130)]
    poly(d, wpt, (145,145,150,255))
    # screws
    for sy in [cy-92, cy+47]:
        ellipse(d, [cx-121,sy-6,cx-104,sy+6], (155,155,160,255), 2)
        d.line([cx-121,sy,cx-104,sy], fill=BLK, width=1)
        d.line([cx-112,sy-6,cx-112,sy+6], fill=BLK, width=1)

    # ── Mounting arm (horizontal) ────────────────────────────────────────────
    arm_b = [(cx-95,cy-80),(cx+2,cy-80),(cx+2,cy-52),(cx-95,cy-52)]
    poly(d, arm_b, (100,102,106,255))
    arm_t = [(cx-95,cy-80),(cx+2,cy-80),(cx-4,cy-98),(cx-101,cy-98)]
    poly(d, arm_t, (118,120,124,255))

    # ── Camera housing ───────────────────────────────────────────────────────
    # front face
    cf = [(cx+2,cy-105),(cx+115,cy-105),(cx+115,cy+20),(cx+2,cy+20)]
    poly(d, cf, (48,48,54,255))
    # top face
    ct = [(cx+2,cy-105),(cx+115,cy-105),(cx+105,cy-128),(cx-8,cy-128)]
    poly(d, ct, (68,68,74,255))
    # right side face
    cs = [(cx+115,cy-105),(cx+105,cy-128),(cx+105,cy+0),(cx+115,cy+20)]
    poly(d, cs, (30,30,36,255))

    # ── Dome lens ────────────────────────────────────────────────────────────
    lx, ly = cx+58, cy-42
    r_outer = 36
    # silver bezel
    ellipse(d, [lx-r_outer,ly-r_outer,lx+r_outer,ly+r_outer], (195,198,205,255))
    # dark lens
    r_lens = 26
    d.ellipse([lx-r_lens,ly-r_lens,lx+r_lens,ly+r_lens], fill=(12,14,22,255))
    # tinted iris ring
    d.ellipse([lx-22,ly-22,lx+22,ly+22], fill=(18,22,40,255))
    # lens centre glint
    d.ellipse([lx-8,ly-8,lx+8,ly+8], fill=(30,35,60,255))
    # specular highlight
    d.ellipse([lx-17,ly-20,lx-7,ly-11], fill=(255,255,255,200))
    d.ellipse([lx+4,ly-8,lx+10,ly-3], fill=(255,255,255,90))

    # ── Status LED ───────────────────────────────────────────────────────────
    ledx, ledy = cx+16, cy-112
    # glow (soft, no outline)
    glow = Image.new("RGBA", img.size, (0,0,0,0))
    gd   = ImageDraw.Draw(glow)
    gd.ellipse([ledx-12,ledy-12,ledx+12,ledy+12], fill=(255,60,60,55))
    img  = Image.alpha_composite(img, glow)
    d    = ImageDraw.Draw(img)
    ellipse(d, [ledx-5,ledy-5,ledx+5,ledy+5], (240,45,45,255), 2)

    # ── Ventilation slots (tiny detail) ─────────────────────────────────────
    for i in range(3):
        sx = cx+80
        sy = cy-88 + i*18
        d.rounded_rectangle([sx,sy,sx+22,sy+7], radius=3, fill=(30,30,36,255))
        d.rounded_rectangle([sx,sy,sx+22,sy+7], radius=3, outline=BLK, width=1)

    img = img.convert("RGB")
    return img

# ─── WAYPOINTS ───────────────────────────────────────────────────────────────

def waypoints():
    img = make_base()
    d   = ImageDraw.Draw(img)

    cx, cy = 256, 268

    # ── Isometric grid tile ──────────────────────────────────────────────────
    tw, th = 200, 100      # tile diamond width / height
    td     = 28            # tile depth (side face height)
    ty     = cy + 60       # tile "equator" y

    # right side face (darkest)
    tr = [(cx,ty+th//2),(cx+tw//2,ty),(cx+tw//2,ty+td),(cx,ty+th//2+td)]
    poly(d, tr, (95,98,103,255))

    # left side face (mid)
    tl = [(cx-tw//2,ty),(cx,ty+th//2),(cx,ty+th//2+td),(cx-tw//2,ty+td)]
    poly(d, tl, (112,115,120,255))

    # top diamond face
    tt = [(cx,ty-th//2),(cx+tw//2,ty),(cx,ty+th//2),(cx-tw//2,ty)]
    poly(d, tt, (148,152,158,255))

    # grid lines on top face — 3×3 divisions
    gc = (118,122,127,255)
    for k in range(1,4):
        t = k/4
        # left-to-right sweep
        p1 = (cx - tw//2 + t*tw//2,  ty - th//2 + t*th//2)
        p2 = (cx            + t*tw//2, ty           + t*th//2)   # wait — let me recalculate
        # correct iso grid: lerp between corners
        # A=top(cx,ty-th//2)  B=right(cx+tw//2,ty)  C=bot(cx,ty+th//2)  D=left(cx-tw//2,ty)
        A=(cx,      ty-th//2)
        B=(cx+tw//2,ty      )
        C=(cx,      ty+th//2)
        Dl=(cx-tw//2,ty     )
        # lines parallel to AC (left-right): lerp D→B and A→C
        p1 = (Dl[0]+(B[0]-Dl[0])*t, Dl[1]+(B[1]-Dl[1])*t)
        p2 = (A[0] +(C[0]-A[0] )*t, A[1] +(C[1]-A[1] )*t)
        d.line([p1,p2], fill=gc, width=2)
        # lines parallel to DB: lerp A→D and B→C
        p3 = (A[0]+(Dl[0]-A[0])*t, A[1]+(Dl[1]-A[1])*t)
        p4 = (B[0]+(C[0]-B[0])*t,  B[1]+(C[1]-B[1])*t)
        d.line([p3,p4], fill=gc, width=2)

    # ── Pin stem ─────────────────────────────────────────────────────────────
    pin_base_y = ty - th//2 - 4   # sits on top surface
    pin_head_cy = cy - 90
    PR = 58                        # pin head radius
    pin_col  = (235,155,20,255)
    pin_hi   = (255,200,75,255)
    pin_sh   = (185,110,10,255)

    # stem (triangle, drawn before head so head covers overlap)
    stem = [(cx-18, pin_head_cy+PR-8),(cx+18, pin_head_cy+PR-8),(cx, pin_base_y)]
    poly(d, stem, pin_col)

    # shadow on right side of stem
    d.polygon([(cx,pin_head_cy+PR-8),(cx+18,pin_head_cy+PR-8),(cx,pin_base_y)],
              fill=pin_sh)

    # ── Pin head ─────────────────────────────────────────────────────────────
    # main circle
    ellipse(d, [cx-PR,pin_head_cy-PR,cx+PR,pin_head_cy+PR], pin_col)

    # highlight crescent (top-left)
    d.arc([cx-PR+10,pin_head_cy-PR+10,cx+PR-22,pin_head_cy+PR-22],
          start=200, end=315, fill=pin_hi, width=10)

    # shadow crescent (bottom-right)
    d.arc([cx-PR+8,pin_head_cy-PR+8,cx+PR-8,pin_head_cy+PR-8],
          start=10, end=155, fill=pin_sh, width=14)

    # redraw outline cleanly over arc smear
    d.ellipse([cx-PR,pin_head_cy-PR,cx+PR,pin_head_cy+PR], outline=BLK, width=OL_W)

    # ── White ring in pin centre ──────────────────────────────────────────────
    wr = 30
    ellipse(d, [cx-wr,pin_head_cy-wr,cx+wr,pin_head_cy+wr], (255,255,255,255), 3)

    # ── Navigation arrow inside white ring ───────────────────────────────────
    # Upward-pointing arrow
    as_ = 14
    arrow_col = (215,140,15,255)
    arrow_pts = [
        (cx,        pin_head_cy-as_),
        (cx+as_,    pin_head_cy+as_//2),
        (cx+as_//2, pin_head_cy+as_//2),
        (cx+as_//2, pin_head_cy+as_+2),
        (cx-as_//2, pin_head_cy+as_+2),
        (cx-as_//2, pin_head_cy+as_//2),
        (cx-as_,    pin_head_cy+as_//2),
    ]
    d.polygon(arrow_pts, fill=arrow_col)
    d.polygon(arrow_pts, outline=BLK, width=1)

    # ── Dotted path on tile surface ──────────────────────────────────────────
    dot_col = (72,75,80,200)
    # Three dots heading right from pin shadow
    for i in range(3):
        dx = cx + 35 + i*28
        # project onto iso tile surface (approximate)
        dy = ty + (dx - cx)*th//tw // 2
        d.ellipse([dx-4,dy-4,dx+4,dy+4], fill=dot_col)

    img = img.convert("RGB")
    return img

# ─── main ────────────────────────────────────────────────────────────────────

out_dir = r"C:\Users\tadsa\Documents\GitHub\03-29_REDO-BUILD\Assets\6. Art\Icons\Category Icons"
os.makedirs(out_dir, exist_ok=True)

lp = loss_prevention()
lp.save(os.path.join(out_dir, "Loss Prevention.png"))
print("Saved Loss Prevention.png")

wp = waypoints()
wp.save(os.path.join(out_dir, "Waypoints.png"))
print("Saved Waypoints.png")
