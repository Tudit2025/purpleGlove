"""Download only the five official CC BY 4.0 MP3s; preserve URLs and hashes."""
from pathlib import Path
import concurrent.futures
import hashlib
import json
import subprocess
import re

ROOT = Path(__file__).resolve().parent
SOURCES = ROOT / "sources"
TRACKS = [
    ("phoenix", "Phoenix (2026)", "phoenix-2026", "2026/03/Phoenix2026.mp3"),
    ("anabasis", "The Weight of Air - Anabasis I", "anabasis-i", "2026/09/Anabasis_I.mp3"),
    ("unraveling", "Unraveling", "unraveling", "2026/05/Unraveling.mp3"),
    ("hiraeth", "Hiraeth", "hiraeth", "2020/06/sb_hiraeth.mp3"),
    ("undertow", "Undertow", "undertow", "2019/12/sb_undertow.mp3"),
]
USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"


def ranged_audio(url, path):
    cache = ROOT.parent.parent / "Temp/music-download" / path.stem
    cache.mkdir(parents=True, exist_ok=True)
    headers = cache / "probe.headers"
    subprocess.run(["curl.exe", "-fLsS", "--max-time", "40", "-A", USER_AGENT, "-r", "0-0", "-D", str(headers),
                    "-o", str(cache / "probe.bin"), url], check=True)
    match = re.search(r"Content-Range:\s*bytes 0-0/(\d+)", headers.read_text(), re.I)
    if not match:
        raise RuntimeError("Server did not return a bounded byte range")
    total = int(match[1])
    size = 3 * 1024 * 1024

    def chunk(start):
        end = min(total, start+size)-1
        part = cache / str(start)
        for attempt in range(5):
            have = part.stat().st_size if part.exists() else 0
            if have == end-start+1:
                return part
            first = start+have
            header, incoming = cache / (str(start)+".headers"), cache / (str(start)+".incoming")
            result = subprocess.run(["curl.exe", "-fLsS", "--max-time", "100", "-A", USER_AGENT, "-r", f"{first}-{end}",
                                     "-D", str(header), "-o", str(incoming), url])
            response = header.read_text() if header.exists() else ""
            if re.search(rf"Content-Range:\s*bytes {first}-{end}/{total}", response, re.I) and incoming.exists():
                data = incoming.read_bytes()
                if len(data) > end-first+1:
                    raise RuntimeError("Incorrect range size")
                with part.open("ab") as output:
                    output.write(data)
                print(path.stem + f" bytes {start}-{end}: {have+len(data)}", flush=True)
                if have+len(data) == end-start+1:
                    return part
        raise RuntimeError("Could not complete range " + str(start))

    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as executor:
        pieces = list(executor.map(chunk, range(0, total, size)))
    data = b"".join(p.read_bytes() for p in pieces)
    assert len(data) == total
    complete = path.with_suffix(path.suffix + ".complete.part")
    complete.write_bytes(data)
    complete.replace(path)


def download(url, path):
    if path.exists() and path.stat().st_size:
        return
    if url.endswith(".mp3"):
        ranged_audio(url, path)
        return
    partial = path.with_suffix(path.suffix + ".part")
    subprocess.run(["curl.exe", "-fLsS", "--retry", "2", "--max-time", "180", "-A",
                    USER_AGENT,
                    "-e", "https://www.scottbuckley.com.au/library/", "-o", str(partial), url], check=True)
    partial.replace(path)


def fetch(track):
    ident, title, slug, media = track
    page = "https://www.scottbuckley.com.au/library/" + slug + "/"
    url = "https://www.scottbuckley.com.au/library/wp-content/uploads/" + media
    path = SOURCES / (ident + ".mp3")
    download(url, path)
    download(page, SOURCES / (ident + ".html"))
    result = dict(id=ident, title=title, author="Scott Buckley", page=page, download=url,
                  license="CC BY 4.0", license_url="https://creativecommons.org/licenses/by/4.0/",
                  file=path.name, sha256=hashlib.sha256(path.read_bytes()).hexdigest(), bytes=path.stat().st_size)
    print(ident + ": " + str(result["bytes"]) + " bytes", flush=True)
    return result


if __name__ == "__main__":
    SOURCES.mkdir(parents=True, exist_ok=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as executor:
        tracks = list(executor.map(fetch, TRACKS))
    download("https://www.scottbuckley.com.au/library/using-this-music/", SOURCES / "using-this-music.html")
    download("https://creativecommons.org/licenses/by/4.0/", SOURCES / "cc-by-4.0.html")
    (ROOT / "sources.json").write_text(json.dumps(tracks, indent=2) + "\n", encoding="utf-8")
