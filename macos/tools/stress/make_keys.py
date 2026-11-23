#!/usr/bin/env python3
"""Chuyển văn bản tiếng Việt thành chuỗi phím Telex (bàn phím US): mỗi token là `mã` hoặc `mã+S` (Shift).
Dùng: make_keys.py text.txt keys.txt expected.txt"""
import sys, unicodedata

LET = dict(a=0,s=1,d=2,f=3,h=4,g=5,z=6,x=7,c=8,v=9,b=11,q=12,w=13,e=14,r=15,y=16,t=17,o=31,u=32,i=34,p=35,l=37,j=38,k=40,n=45,m=46)
DIG = {'1':18,'2':19,'3':20,'4':21,'5':23,'6':22,'7':26,'8':28,'9':25,'0':29}
PUN = {'-':27,'=':24,'[':33,']':30,'\\':42,';':41,"'":39,',':43,'.':47,'/':44,'`':50,' ':49}
SHF = {'!':'1','@':'2','#':'3','$':'4','%':'5','^':'6','&':'7','*':'8','(':'9',')':'0','_':'-','+':'=','{':'[','}':']','|':'\\',':':';','"':"'",'<':',','>':'.','?':'/','~':'`'}
TONE = {'\u0300':'f','\u0301':'s','\u0309':'r','\u0303':'x','\u0323':'j'}
MARK = {('a','\u0302'):'aa',('e','\u0302'):'ee',('o','\u0302'):'oo',('a','\u0306'):'aw',('o','\u031b'):'ow',('u','\u031b'):'uw'}

def tok(ch):
    t = tok_raw(ch)
    return t if t.startswith('u') or ch == '\n' else f"{t}@{ord(ch):x}"

def tok_raw(ch):
    if ch == '\n': return '36'
    if ch.isalpha() and ch.isascii():
        return f"{LET[ch.lower()]}" + ('+S' if ch.isupper() else '')
    if ch in DIG: return str(DIG[ch])
    if ch in PUN: return str(PUN[ch])
    if ch in SHF: return tok_raw(SHF[ch]).split('+')[0] + '+S'
    return f"u{ord(ch):x}"  # ký tự không có trên bàn phím US (—, ±…): gửi bằng sự kiện Unicode

CHARS = {}
def with_char(token, ch):
    return f"{token}@{ord(ch):x}"

TONE_KEYS = {
    'telex': {'\u0300': 'f', '\u0301': 's', '\u0309': 'r', '\u0303': 'x', '\u0323': 'j'},
    'vni':   {'\u0300': '2', '\u0301': '1', '\u0309': '3', '\u0303': '4', '\u0323': '5'},
    'viqr':  {'\u0300': '`', '\u0301': "'", '\u0309': '?', '\u0303': '~', '\u0323': '.'},
}
MARK_KEYS = {
    'telex': {('a','\u0302'):'aa',('e','\u0302'):'ee',('o','\u0302'):'oo',('a','\u0306'):'aw',('o','\u031b'):'ow',('u','\u031b'):'uw', 'đ':'dd'},
    'vni':   {('a','\u0302'):'a6',('e','\u0302'):'e6',('o','\u0302'):'o6',('a','\u0306'):'a8',('o','\u031b'):'o7',('u','\u031b'):'u7', 'đ':'d9'},
    'viqr':  {('a','\u0302'):'a^',('e','\u0302'):'e^',('o','\u0302'):'o^',('a','\u0306'):'a(',('o','\u031b'):'o+',('u','\u031b'):'u+', 'đ':'dd'},
}

def encode_word(word, method):
    keys, tone = [], None
    tones, marks = TONE_KEYS[method], MARK_KEYS[method]
    for ch in word:
        nfd = unicodedata.normalize('NFD', ch)
        base, ms = nfd[0], nfd[1:]
        up = base.isupper()
        lb = base.lower()
        if ch in 'đĐ':
            seq = marks['đ']
        else:
            seq = lb
            for m in ms:
                if m in tones: tone = tones[m]
                elif (lb, m) in marks: seq = marks[(lb, m)]
                else: raise SystemExit(f"dấu lạ {ch!r}")
        keys += [tok(c.upper() if (up and c.isalpha()) else c) for c in seq]
    if tone:
        keys.append(tok(tone.upper() if method == 'telex' and word.isupper() and len(word) > 1 else tone))
    return keys

def main(src, keys_out, expected_out, method='telex'):
    text = unicodedata.normalize('NFC', open(src, encoding='utf8').read().rstrip('\n'))
    if method == 'viqr':
        # VIQR dùng ' ` ? ~ . ^ ( + - làm phím dấu nên dấu câu dính liền từ sẽ bị hiểu là dấu thanh (VIQR chuẩn dùng \\ để thoát,
        # engine chưa hỗ trợ). Với VIQR chỉ kiểm tra chữ: thay mọi dấu câu/chữ số bằng khoảng trắng.
        text = ''.join(c if (c.isalpha() or c in ' \n') else ' ' for c in text)
    keys, word = [], ''
    for ch in text:
        if ch.isalpha():
            word += ch
        else:
            if word: keys += encode_word(word, method); word = ''
            keys.append(tok(ch))
    if word: keys += encode_word(word, method)
    open(keys_out, 'w').write(' '.join(keys))
    open(expected_out, 'w', encoding='utf8').write(text)
    print(method, len(text), 'ký tự →', len(keys), 'phím')

main(*sys.argv[1:5])
