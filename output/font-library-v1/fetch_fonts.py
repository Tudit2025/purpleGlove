"""Download upstream, unmodified static fonts and their licenses.

Run from any directory with Python 3.11+. Only writes inside this library.
The first run pins repository revisions; subsequent runs reuse sources.lock.json.
"""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from urllib.request import Request, urlopen
from urllib.parse import quote
import hashlib
import json
import zipfile

ROOT = Path(__file__).resolve().parent
DATE = "2026-10-31"  # Research date, Asia/Hong_Kong.

FAMILIES = [
    dict(id="eb-garamond", name="EB Garamond", language="en",
         repo="octaviopardo/EBGaramond12", ref="master", license="OFL.txt",
         files=["fonts/otf/EBGaramond-Regular.otf", "fonts/otf/EBGaramond-Medium.otf",
                "fonts/otf/EBGaramond-Italic.otf"],
         notes="Formal document body; Medium for dim paper; Italic for long letters."),
    dict(id="im-fell-english", name="IM FELL English", language="en",
         repo="google/fonts", ref="main", license="ofl/imfellenglish/OFL.txt",
         files=["ofl/imfellenglish/IMFeENrm28P.ttf"],
         notes="Old printed prayer books. Regular only for indexed puzzle lines."),
    dict(id="courgette", name="Courgette", language="en",
         repo="google/fonts", ref="main", license="ofl/courgette/OFL.txt",
         files=["ofl/courgette/Courgette-Regular.ttf"],
         notes="Short personal handwriting; never mandatory cipher glyphs."),
    dict(id="cinzel", name="Cinzel", language="en",
         repo="NDISCOVER/Cinzel", ref="master", license="OFL.txt",
         files=["fonts/ttf/Cinzel-Regular.ttf"],
         notes="Large cover and institutional headings only, not body text."),
    dict(id="ibm-plex-mono", name="IBM Plex Mono", language="en",
         repo="google/fonts", ref="main", license="ofl/ibmplexmono/OFL.txt",
         files=["ofl/ibmplexmono/IBMPlexMono-Regular.ttf"],
         notes="Optional accessible transcription and decoding grid, not historical printing."),
    dict(id="source-han-serif", name="Source Han Serif CN / 思源宋体", language="zh-Hans",
         repo="adobe-fonts/source-han-serif", ref="release", license="LICENSE.txt",
         files=["SubsetOTF/CN/SourceHanSerifCN-Regular.otf",
                "SubsetOTF/CN/SourceHanSerifCN-SemiBold.otf"],
         notes="Official upstream CN regional subset; body and institutional headings."),
    dict(id="zhuque-fangsong", name="Zhuque Fangsong / 朱雀仿宋", language="zh-Hans",
         repo="TrionesType/zhuque", ref="v0.212", license="LICENSE.txt",
         files=[], archive="https://github.com/TrionesType/zhuque/releases/download/v0.212/ZhuqueFangsong-v0.212.zip",
         archive_member="ZhuqueFangsong-Regular.ttf",
         notes="Technical preview v0.212; old book and ledger body; use Source Han Serif fallback."),
    dict(id="lxgw-wenkai", name="LXGW WenKai / 霞鹜文楷", language="zh-Hans",
         repo="lxgw/LxgwWenKai", ref="v1.530", license="OFL.txt",
         files=["fonts/TTF/LXGWWenKai-Regular.ttf"],
         notes="Personal handwriting and notebook; unmodified full Regular file."),
    dict(id="source-han-sans", name="Source Han Sans CN / 思源黑体", language="zh-Hans",
         repo="adobe-fonts/source-han-sans", ref="release", license="LICENSE.txt",
         files=["SubsetOTF/CN/SourceHanSansCN-Regular.otf"],
         notes="Optional accessible reading, translation, input and subtitles."),
]


def read_url(url):
    request = Request(url, headers={"User-Agent": "PurpleGlove-font-research/1.0"})
    with urlopen(request, timeout=45) as response:
        return response.read()


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def json_write(path, obj):
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def pin_revisions():
    lock_path = ROOT / "sources.lock.json"
    if lock_path.exists():
        return json.loads(lock_path.read_text(encoding="utf-8"))
    requests = sorted({(f["repo"], f["ref"]) for f in FAMILIES})

    def resolve(pair):
        repo, ref = pair
        data = json.loads(read_url(f"https://api.github.com/repos/{repo}/commits/{quote(ref)}"))
        return f"{repo}@{ref}", data["sha"]

    with ThreadPoolExecutor(max_workers=5) as pool:
        revisions = dict(pool.map(resolve, requests))
    json_write(lock_path, revisions)
    return revisions


def collect(family, revisions):
    fid = family["id"]
    dest = ROOT / "fonts" / fid
    dest.mkdir(parents=True, exist_ok=True)
    source = ROOT / "sources" / fid
    source.mkdir(parents=True, exist_ok=True)
    commit = revisions[f"{family['repo']}@{family['ref']}"]
    base = f"https://raw.githubusercontent.com/{family['repo']}/{commit}/"

    def download(url, path):
        if path.exists():
            return path.read_bytes()
        data = read_url(url)
        path.write_bytes(data)
        return data

    license_url = base + family["license"]
    license_data = download(license_url, dest / "OFL.txt")
    assert b"SIL OPEN FONT LICENSE" in license_data.upper(), fid
    font_entries = []

    def register(path, data, url, member=None):
        assert data[:4] in (b"OTTO", b"\x00\x01\x00\x00"), f"Invalid font: {path}"
        entry = dict(path=path.relative_to(ROOT).as_posix(), bytes=len(data),
                     sha256=sha256(data), url=url, modified=False)
        if member:
            entry["archive_member"] = member
        font_entries.append(entry)

    for file_path in family["files"]:
        url = base + quote(file_path, safe="/")
        path = dest / Path(file_path).name
        data = download(url, path)
        register(path, data, url)
    if family.get("release_font"):
        url = family["release_font"]
        path = dest / url.rsplit("/", 1)[-1]
        register(path, download(url, path), url)
    if family.get("archive"):
        url = family["archive"]
        archive = source / url.rsplit("/", 1)[-1]
        archive_data = download(url, archive)
        member = family["archive_member"]
        with zipfile.ZipFile(archive) as bundle:
            data = bundle.read(member)
        path = dest / Path(member).name
        path.write_bytes(data)
        register(path, data, url, member)
        json_write(source / "archive.json", dict(url=url, sha256=sha256(archive_data)))

    metadata = dict(id=fid, name=family["name"], language=family["language"],
                    source_page=f"https://github.com/{family['repo']}",
                    source_revision=commit, source_ref=family["ref"],
                    license="OFL-1.1", license_url=license_url,
                    license_path=(dest / "OFL.txt").relative_to(ROOT).as_posix(),
                    license_sha256=sha256(license_data), collected_date=DATE,
                    notes=family["notes"], files=font_entries)
    json_write(source / "source.json", metadata)
    print(f"OK {fid}: {len(font_entries)} font files", flush=True)
    return metadata


def main():
    revisions = pin_revisions()
    with ThreadPoolExecutor(max_workers=5) as pool:
        families = list(pool.map(lambda f: collect(f, revisions), FAMILIES))
    json_write(ROOT / "manifest.json", dict(research_date=DATE, timezone="Asia/Hong_Kong",
               source_script="../../doc/ph-revised.md", families=families))
    print("Total font bytes:", sum(x["bytes"] for f in families for x in f["files"]))


if __name__ == "__main__":
    main()
