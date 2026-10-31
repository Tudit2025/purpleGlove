"""Generate vector prop drawings and verify them against the revised script.

Run with Python 3.11 from any working directory. Uses existing workspace Pillow,
fontTools (Temp/ph-font-tools), and the collected font library; no downloads.
"""
from pathlib import Path
import html
import json
import math
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[1]
sys.path.insert(0, str(PROJECT / 'Temp/ph-font-tools'))
from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen
from PIL import Image, ImageDraw, ImageFont

ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'
AFFINE = ''.join(ALPHABET[21 * j % 26] for j in range(26))
FONT_DIR = PROJECT / 'output/font-library-v1/fonts'
FONT_PATHS = {
    'sans': FONT_DIR / 'source-han-sans/SourceHanSansCN-Regular.otf',
    'mono': FONT_DIR / 'ibm-plex-mono/IBMPlexMono-Regular.ttf',
}
FONTS = {name: TTFont(path) for name, path in FONT_PATHS.items()}
GLYPHS = {name: font.getGlyphSet() for name, font in FONTS.items()}
CMAPS = {name: font.getBestCmap() for name, font in FONTS.items()}
INK = '#302c27'
MUTED = '#766b5b'
GOLD = '#a57d43'
PAPER = '#f7f2e7'


def xy(cx, cy, r, step):
    angle = step * math.tau / 26 - math.pi / 2
    return cx + r * math.cos(angle), cy + r * math.sin(angle)


class Drawing:
    """Millimetre drawing commands shared by outlined SVG and review PNG."""
    def __init__(self, width, height, title, bg='#ffffff', scale=4):
        self.w, self.h, self.title, self.scale = width, height, title, scale
        self.svg = []
        self.defs = {}
        self.img = Image.new('RGB', (round(width * scale), round(height * scale)), bg)
        self.draw = ImageDraw.Draw(self.img)
        self.pil_fonts = {}
        self.rect(0, 0, width, height, bg, None)

    def pt(self, x, y):
        return (x * self.scale, y * self.scale)

    def attrs(self, fill, stroke, sw):
        return f'fill="{fill or "none"}" stroke="{stroke or "none"}" stroke-width="{sw}"'

    def rect(self, x, y, w, h, fill=None, stroke=INK, sw=.3, dashed=False):
        dash = ' stroke-dasharray="2 1"' if dashed else ''
        self.svg.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" {self.attrs(fill,stroke,sw)}{dash}/>')
        self.draw.rectangle([self.pt(x,y), self.pt(x+w,y+h)], fill=fill, outline=stroke,
                            width=max(1,round(sw*self.scale)))

    def line(self, x1, y1, x2, y2, color=INK, sw=.3):
        self.svg.append(f'<path d="M{x1:.4f},{y1:.4f} L{x2:.4f},{y2:.4f}" fill="none" stroke="{color}" stroke-width="{sw}"/>')
        self.draw.line([self.pt(x1,y1),self.pt(x2,y2)],fill=color,width=max(1,round(sw*self.scale)))

    def poly(self, points, fill=None, stroke=INK, sw=.35, closed=False):
        name = 'polygon' if closed else 'polyline'
        coords = ' '.join(f'{x:.4f},{y:.4f}' for x,y in points)
        self.svg.append(f'<{name} points="{coords}" {self.attrs(fill,stroke,sw)} stroke-linejoin="round"/>')
        pts = [self.pt(*p) for p in points]
        if fill:
            self.draw.polygon(pts, fill=fill)
        if stroke:
            if closed:
                pts.append(pts[0])
            self.draw.line(pts,fill=stroke,width=max(1,round(sw*self.scale)))

    def circle(self, cx, cy, r, fill=None, stroke=INK, sw=.3):
        self.svg.append(f'<circle cx="{cx}" cy="{cy}" r="{r}" {self.attrs(fill,stroke,sw)}/>')
        self.draw.ellipse([self.pt(cx-r,cy-r),self.pt(cx+r,cy+r)],fill=fill,outline=stroke,
                          width=max(1,round(sw*self.scale)))

    def text(self, x, y, value, size=3.2, color=INK, align='left', font='sans'):
        value = str(value)
        f = FONTS[font]
        upem = f['head'].unitsPerEm
        names = []
        for char in value:
            assert ord(char) in CMAPS[font], f'Missing glyph: {char!r} / {font}'
            names.append(CMAPS[font][ord(char)])
        advances = [f['hmtx'][name][0] * size / upem for name in names]
        total = sum(advances)
        if align == 'center':
            x -= total/2
        elif align == 'right':
            x -= total
        font_px = max(1,round(size*self.scale))
        key = (font,font_px)
        if key not in self.pil_fonts:
            self.pil_fonts[key] = ImageFont.truetype(str(FONT_PATHS[font]),font_px)
        pieces = []
        for char, name, advance in zip(value,names,advances):
            identifier = f'{font}_{ord(char):x}'
            if identifier not in self.defs:
                pen = SVGPathPen(GLYPHS[font])
                GLYPHS[font][name].draw(pen)
                self.defs[identifier] = f'<path id="{identifier}" d="{pen.getCommands()}"/>'
            pieces.append(f'<use href="#{identifier}" transform="translate({x:.4f} {y:.4f}) scale({size/upem:.8f} {-size/upem:.8f})"/>')
            self.draw.text(self.pt(x,y),char,fill=color,font=self.pil_fonts[key],anchor='ls')
            x += advance
        self.svg.append(f'<g fill="{color}" aria-label="{html.escape(value,quote=True)}">'+''.join(pieces)+'</g>')

    def save(self, name):
        data = (f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.w}mm" height="{self.h}mm" '
                f'viewBox="0 0 {self.w} {self.h}" role="img"><title>{html.escape(self.title)}</title>'
                '<defs>'+''.join(self.defs.values())+'</defs>'+''.join(self.svg)+'</svg>')
        path = ROOT / f'{name}.svg'
        path.write_text(data,encoding='utf-8')
        ET.parse(path)
        self.img.save(ROOT / f'{name}.png')


def header(d, number, title, subtitle):
    d.text(13,13,f'{number}  /  {title}',4.5)
    d.text(13,20,subtitle,2.6,MUTED)


def outer(d, cx, cy):
    d.circle(cx,cy,85,PAPER,INK,.45)
    d.circle(cx,cy,66,None,GOLD,.3)
    for j, letter in enumerate(ALPHABET):
        d.line(*xy(cx,cy,66,j+.5),*xy(cx,cy,85,j+.5),GOLD,.2)
        x,y = xy(cx,cy,75,j)
        d.text(x,y+1.5,letter,4.7,align='center',font='mono')
        x,y = xy(cx,cy,81.3,j)
        d.text(x,y+.7,str(j),2.1,MUTED,align='center',font='mono')
    d.circle(cx,cy,1.5,None,INK,.3)
    d.line(cx-3,cy,cx+3,cy,MUTED,.15)
    d.line(cx,cy-3,cx,cy+3,MUTED,.15)


def inner(d, cx, cy, alphabet=ALPHABET, shift=0, mode='普通盘  a=1'):
    d.circle(cx,cy,65,'#eee2c8',INK,.45)
    d.circle(cx,cy,49,None,GOLD,.25)
    for j, letter in enumerate(alphabet):
        step = j+shift
        d.line(*xy(cx,cy,49,step+.5),*xy(cx,cy,65,step+.5),GOLD,.2)
        x,y = xy(cx,cy,57.7,step)
        d.text(x,y+1.5,letter,4.5,align='center',font='mono')
        x,y = xy(cx,cy,51.9,step)
        d.text(x,y+.65,ALPHABET.index(letter),2.0,MUTED,align='center',font='mono')
    p = xy(cx,cy,64,shift)
    q = xy(cx,cy,61.5,shift-.16)
    r = xy(cx,cy,61.5,shift+.16)
    d.poly([p,q,r],GOLD,None,closed=True)
    d.text(cx,cy-21,mode,3.7,align='center')
    d.text(cx,cy-13,'内盘：明文',3.2,align='center')
    d.text(cx,cy+15,'加密：内 → 外',3,align='center')
    d.text(cx,cy+22,'解密：外 → 内',3,align='center')
    d.text(cx,cy+32,'用 A 旁三角对准外盘密钥',2.5,MUTED,align='center')
    d.circle(cx,cy,1.5,'#ffffff',INK,.3)


PIG_PATHS = [
    [(1,-1),(1,1),(-1,1)],
    [(-1,-1),(-1,1),(1,1),(1,-1)],
    [(-1,-1),(-1,1),(1,1)],
    [(-1,-1),(1,-1),(1,1),(-1,1)],
    [(-1,-1),(1,-1),(1,1),(-1,1),(-1,-1)],
    [(1,-1),(-1,-1),(-1,1),(1,1)],
    [(-1,-1),(1,-1),(1,1)],
    [(-1,1),(-1,-1),(1,-1),(1,1)],
    [(-1,1),(-1,-1),(1,-1)],
]
ANGLE_PATHS = [
    [(-1,-1),(0,1),(1,-1)],
    [(-1,-1),(1,0),(-1,1)],
    [(1,-1),(-1,0),(1,1)],
    [(-1,1),(0,-1),(1,1)],
]


def pig(d, x, y, letter, radius=2.8):
    index = ALPHABET.index(letter)
    if index < 18:
        points = PIG_PATHS[index % 9]
        dotted = index >= 9
    else:
        points = ANGLE_PATHS[(index-18) % 4]
        dotted = index >= 22
    d.poly([(x+a*radius,y+b*radius) for a,b in points],stroke=INK,sw=.45)
    if dotted:
        d.circle(x,y,.55,INK,None)


def rail_grid(d, x, y, cols=41, cell=4.5, top='', bottom='', indices=True):
    height=16
    d.rect(x,y,cols*cell,height,'#ffffff',INK,.3)
    d.line(x,y+8,x+cols*cell,y+8,INK,.25)
    for i in range(cols):
        if i:
            d.line(x+i*cell,y,x+i*cell,y+height,INK,.2)
        if indices:
            d.text(x+(i+.5)*cell,y+2.1,2*i+1,1.45,MUTED,align='center',font='mono')
            d.text(x+(i+.5)*cell,y+10.1,2*i+2,1.45,MUTED,align='center',font='mono')
        if i<len(top):
            d.text(x+(i+.5)*cell,y+6.5,top[i],3.2,align='center',font='mono')
        if i<len(bottom):
            d.text(x+(i+.5)*cell,y+14.5,bottom[i],3.2,align='center',font='mono')


def build_prints():
    d=Drawing(210,297,'译密板 A4 / 外盘')
    header(d,'01','固定外盘','A4 · 100% 实际尺寸 · 顺时针 A—Z · 外盘始终读密文')
    outer(d,105,115)
    d.text(105,105,'固定在底板上的外盘',4,align='center')
    d.text(105,135,'剪外圆；中心打孔 3 mm',3.2,align='center')
    d.text(13,214,'装配：外盘直径 170 mm，内盘直径 130 mm。',3.1)
    d.text(13,222,'一次只装一张内盘；用垫片调节阻力，避免读字时滑动。',3.1)
    d.text(13,230,'设钥：内盘 A 的三角标记，对准外盘密钥字母。',3.1)
    d.text(13,238,'普通盘对 D：ABC → DEF。五倍盘对 I：ABC → INS。',3.1)
    d.line(14,255,64,255,INK,.5)
    d.line(14,252,14,258,INK,.4)
    d.line(64,252,64,258,INK,.4)
    d.text(39,265,'50 mm 校准线',3,align='center')
    d.text(13,283,'先量校准线，再裁切；打印时关闭“适合页面”。',2.7,MUTED)
    d.save('print-01-outer')

    d=Drawing(210,297,'译密板 A4 / 可换内盘')
    d.text(13,11,'02  /  两张可换内盘 · 剪外圆，中心孔 3 mm',3.7)
    inner(d,105,82)
    inner(d,105,219,AFFINE,mode='五倍盘  a=5')
    d.save('print-02-inner')

    d=Drawing(210,297,'译密板 A4 / 附属工具')
    header(d,'03','对照牌与取字工具','实线外框裁切；遮行框的虚线内窗挖空；密钥条由玩家填写')
    d.text(13,31,'猪圈对照牌 · 采用剧本附录 D 的朝向与圆点',3.2)
    d.rect(13,36,182,39,PAPER,INK,.35)
    for index,letter in enumerate(ALPHABET):
        col,row=index%13,index//13
        x,y=20+14*col,44+19*row
        pig(d,x,y,letter)
        d.text(x,y+8,letter,3.3,align='center',font='mono')
    d.text(13,88,'原页遮行框 · 先找到指定印刷行，不改变原书字距',3.2)
    d.rect(13,94,184,23,PAPER,INK,.35)
    d.rect(18,99,174,8,'#ffffff',MUTED,.3,dashed=True)
    d.text(105,114,'只露出正在查阅的原书行；版本与页号仍要核对',2.4,align='center')
    d.text(13,131,'取字条 · 从原书抄入 A—Z，跳过空格与标点',3.2)
    d.rect(13,137,184,26,PAPER,INK,.35)
    cell=184/36
    for i in range(36):
        x=13+i*cell
        d.line(x,137,x,155,INK,.2)
        d.text(x+cell/2,142,i+1,2.1,MUTED,align='center',font='mono')
    d.line(13,144,197,144,INK,.2)
    d.line(13,155,197,155,INK,.25)
    d.text(16,161,'版次：__________  页：______  印刷行：______',2.6)
    d.text(13,173,'取字从 1 开始；这是转录用格条，不能直接当比例字体的量尺。',2.6,MUTED)
    d.text(13,190,'循环密钥条 · 木夹或纸夹停在当前字母处',3.2)
    d.rect(13,196,110,25,PAPER,INK,.35)
    for i in range(4):
        x=16+i*25
        d.rect(x,202,22,16,'#ffffff',INK,.25)
        d.text(x+11,200,i+1,2,MUTED,align='center',font='mono')
    d.text(133,205,'4 → 1 循环',3)
    d.text(133,213,'一字一换钥',3)
    d.text(13,238,'使用顺序',3.5)
    d.text(13,248,'书本：核对版次 → 定位印刷行 → 去空格转录 → 按号取字。',3)
    d.text(13,257,'猪圈：形状、朝向、圆点共同决定字母。',3)
    d.text(13,266,'维吉尼亚：填关键词 → 设置转盘 → 读一个字 → 移动木夹。',3)
    d.text(13,283,'整套部件从第一幕即可查看；不预填本案关键词。',2.7,MUTED)
    d.save('print-03-accessories')

    d=Drawing(210,297,'译密板 A4 / 两行排字条')
    header(d,'04','两行排字条','41 列 × 上下两格 = 82 字；小数字是还原后的明文位置')
    d.rect(12,31,187,41,PAPER,INK,.35)
    d.text(14,38,'上行：密文前半段，从左至右填入',2.8)
    rail_grid(d,13.25,42)
    d.text(14,65,'下行：密文后半段；读出时每列上、下，再往右一列',2.6)
    d.text(13,88,'解密不是沿上行读完再读下行：那样会得到原来的密文。',3)
    d.text(13,103,'练习：HLOOLELWRD，长度 10，分成两段各 5 字。',3)
    rail_grid(d,25,114,cols=5,cell=14,top='HLOOL',bottom='ELWRD')
    for i in range(5):
        cx=25+(i+.5)*14
        d.line(cx,133,cx,138,GOLD,.4)
        if i<4:
            d.line(cx,138,cx+14,133,GOLD,.4)
    d.text(109,121,'先填：HLOOL / ELWRD',3)
    d.text(109,130,'再读：H E L L O W O R L D',2.9)
    d.text(13,155,'1. 数清 N：上行 ceil(N/2) 字，下行 floor(N/2) 字。',3)
    d.text(13,165,'2. 完整拆成两段，分别抄入上行、下行。',3)
    d.text(13,175,'3. 按每列上、下读出，再读下一列；最后恢复断句。',3)
    d.text(13,185,'4. 加密反过来填格，再抄完整上行、完整下行。',3)
    d.text(13,205,'奇数长度：最后一个下格留空，不补字，不丢字。',3.1)
    d.text(13,215,'长信应整体分组；不要先切成小块，再各自解密。',3.1)
    d.text(13,239,'本剧第四幕密信：N=82，上行 41，下行 41。',3.2)
    d.text(13,249,'本条只处理两行栅栏；地点短句使用另一张仿射密文。',3)
    d.text(13,283,'排字条可以夹在工作板上，或独立摆在信纸下方。',2.7,MUTED)
    d.save('print-04-rails')


def build_overview():
    d=Drawing(480,350,'马泰奥的译密板 / 结构提案',bg='#e6dfd1')
    d.text(35,22,'马泰奥的译密板',7)
    d.text(35,33,'折叠木匣 / 可换字母盘 / 查书与排字附件',3.4,MUTED)
    d.text(445,25,'PH  /  PROP STUDY 01',3.5,MUTED,align='right',font='mono')
    d.rect(42,48,400,240,'#a99b86',None)
    d.rect(38,44,400,240,'#574333',INK,.8)
    d.rect(43,49,190,230,'#735a43','#af916b',.4)
    d.rect(243,49,190,230,'#735a43','#af916b',.4)
    d.rect(234,45,8,238,'#3a3129',None)
    for y in [83,231]:
        d.rect(230,y,16,15,'#a08250',INK,.3)
        for x in [233,243]:
            d.circle(x,y+7.5,.7,INK,None)
    outer(d,138,150)
    inner(d,138,150,shift=3)
    d.circle(138,150,3,'#aa854e',INK,.3)
    d.line(136.5,150,139.5,150,INK,.4)
    d.text(138,250,'A  /  同一半径，双向读字',3.5,'#f7eedf',align='center')
    d.text(138,261,'凯撒 · 维吉尼亚 · 换盘后的仿射',3,'#f7eedf',align='center')
    d.rect(254,65,169,118,PAPER,INK,.3)
    d.rect(313,60,50,9,'#b59864',INK,.3)
    d.text(263,80,'B  /  工作纸与密钥条',4)
    d.text(263,91,'密钥：D  /  位移 3',3.2)
    d.text(263,104,'NHHS WKH RULJLQDO',4,font='mono')
    d.text(263,116,'KEEP THE ORIGINAL',4,font='mono')
    d.line(262,123,414,123,'#c7bba6',.2)
    d.text(263,134,'明文：保留原件。',3.4)
    d.text(263,145,'来源：仍须独立核验。',3.4)
    d.text(263,163,'转盘展示：内盘 A 对准外盘 D',2.8,MUTED)
    d.rect(254,191,169,32,'#d2c1a0',INK,.3)
    d.text(260,200,'C  /  对照牌与取字条收纳',3.3)
    for i,ch in enumerate('ABCDEF'):
        x=265+15*i
        pig(d,x,210,ch,2)
        d.text(x,218,ch,2.6,align='center',font='mono')
    d.text(358,211,'完整 26 字母',2.7)
    d.text(358,218,'可取出查看',2.7)
    d.rect(254,233,169,33,PAPER,INK,.3)
    d.text(260,242,'D  /  两行排字条（图示为局部）',3.1)
    rail_grid(d,261,246,cols=20,cell=7.7,indices=False)
    d.text(38,301,'A  转动或更换内盘；始终“外密内明”。',3.4)
    d.text(250,301,'B  每次抄录保留所用密钥与版本。',3.4)
    d.text(38,312,'C  查书、认符号；附件从开场就存在。',3.4)
    d.text(250,312,'D  完整纸样上下各 41 格，交错读出。',3.4)
    d.text(38,334,'展开约 400×240 mm；合起约 200×240×24 mm。结构提案，非生产工程图。',3,MUTED)
    d.save('overview')


def normalize(value):
    return re.sub('[^A-Z]','',value.upper())


def verify_script():
    script=(PROJECT/'doc/ph-revised.md').read_text(encoding='utf-8')
    def section(start,end):
        return script.split(start,1)[1].split(end,1)[0]
    def blocks(part):
        return [b.strip() for b in re.findall(r'(?:^    [^\n]+\n?)+',part,re.M)]
    report={'script':'doc/ph-revised.md','checks':[]}
    def check(name,actual,expected,**extra):
        assert actual==expected, f'{name}: {actual!r} != {expected!r}'
        report['checks'].append({'name':name,'passed':True,**extra})
    caesar=blocks(section('### 谜题一：','### 首次门边交谈'))
    c,p=map(normalize,caesar[:2])
    enc=lambda p,k: ''.join(ALPHABET[(ALPHABET.index(ch)+k)%26] for ch in p)
    check('Caesar script ciphertext',enc(p,3),c,letters=len(c))
    check('Caesar script plaintext',enc(c,-3),p)

    book=blocks(section('### 谜题二：','### 玩家主动调档'))
    coords=[tuple(map(int,m)) for m in re.findall(r'(\d+)-(\d+)-(\d+)',book[0])]
    slices=section('### C. 书本密码的完整取字切片','### D. 猪圈密码的完整对照表')
    rows=re.findall(r'\| (\d+)—(\d+) \| ([A-Z ]+) \|',slices)
    tables=[{(int(p),int(l)):normalize(t) for p,l,t in rows[i:i+3]} for i in [0,3]]
    for table,label,expected in zip(tables,['Seven-petal book','Ordinary book'],
                                    [normalize(book[1]),'CVSLELSKSNOLOHHR']):
        out=''.join(table[p,l][i-1] for p,l,i in coords)
        check(label,out,expected,letters=len(out),output=out)
        assert max(map(len,table.values()))<=36

    pigpart=section('### 谜题三：','### 贝阿特丽切的隐瞒')
    pigblocks=blocks(pigpart)
    mapping={}
    pigtable=section('### D. 猪圈密码的完整对照表','### E. 笔记本教案的最低完整内容')
    for a,s,b,t in re.findall(r'\| ([A-Z]) \| ([^|]+) \| ([A-Z]) \| ([^|]+) \|',pigtable):
        mapping[s.strip()]=a
        mapping[t.strip()]=b
    tokens=pigblocks[0].split()
    pigtext=''.join(mapping[t] for t in tokens if t!='/')
    check('Pigpen script plaintext',pigtext,normalize(pigblocks[1]),letters=len(pigtext))
    # Check explicit drawn geometry assignment against the script's symbol table.
    symbols=['┘','⊔','└','⊐','□','⊏','┐','⊓','┌']
    symbols += [s+'·' for s in symbols]
    symbols += ['∨','〉','〈','∧','∨·','〉·','〈·','∧·']
    check('Pigpen drawn letter order',''.join(mapping[s] for s in symbols),ALPHABET)

    v=blocks(section('### 谜题四：','### 第一项正式判断'))
    c,p=map(normalize,v[:2])
    key=[ALPHABET.index(ch) for ch in 'SOIE']
    e=''.join(ALPHABET[(ALPHABET.index(ch)+key[i%4])%26] for i,ch in enumerate(p))
    dec=''.join(ALPHABET[(ALPHABET.index(ch)-key[i%4])%26] for i,ch in enumerate(c))
    check('Vigenere script ciphertext',e,c,letters=len(c))
    check('Vigenere script plaintext',dec,p)

    a=blocks(section('### 谜题五：','### 谜题六：'))
    c,p=normalize(a[0]),normalize(a[-1])
    check('Affine script ciphertext',''.join(ALPHABET[(5*ALPHABET.index(ch)+8)%26] for ch in p),c,letters=len(c))
    check('Affine script plaintext',''.join(ALPHABET[21*(ALPHABET.index(ch)-8)%26] for ch in c),p)

    rails=blocks(section('### 谜题六：','### 明文附记'))
    c,p=map(normalize,rails[:2])
    check('Rail script ciphertext',p[::2]+p[1::2],c,letters=len(c),top=(len(c)+1)//2,bottom=len(c)//2)
    def rail_decode(c):
        split=(len(c)+1)//2
        top,bottom=c[:split],c[split:]
        return ''.join(top[i]+(bottom[i] if i<len(bottom) else '') for i in range(len(top)))
    check('Rail script plaintext',rail_decode(c),p)
    check('Rail odd-length handling',rail_decode('HLOEL'),'HELLO')
    check('Rail blank handling',rail_decode(''),'')
    assert len(c)==82

    for mult,inner_order in [(1,ALPHABET),(5,AFFINE)]:
        pairs=0
        for shift in range(26):
            for x,ch in enumerate(ALPHABET):
                # Read the actual printed inner slot after a physical rotation.
                slot=inner_order.index(ch)
                outer_index=(slot+shift)%26
                assert outer_index==(mult*x+shift)%26
                assert inner_order[(outer_index-shift)%26]==ch
                pairs+=1
        report['checks'].append({'name':f'Physical a={mult} disk, every shift, both directions',
                                 'passed':True,'letter_shift_pairs':pairs,'clockwise_inner':inner_order})
    report['status']='passed'
    report['limits']=['No physical print or assembly test yet.','No Unity integration or in-engine readability test.']
    return report


if __name__=='__main__':
    report=verify_script()
    build_prints()
    build_overview()
    report['svg_pages']=[{'file':p.name,'xml_valid':True} for p in sorted(ROOT.glob('*.svg'))]
    (ROOT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(f'Generated 4 A4 templates and 1 overview. {len(report["checks"])} checks passed.')
