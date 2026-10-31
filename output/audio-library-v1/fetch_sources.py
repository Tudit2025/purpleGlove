"""Fetch the public CC0 audio sources and retain provenance beside the originals."""
from concurrent.futures import ThreadPoolExecutor
from hashlib import sha256
from pathlib import Path
from urllib.request import Request, urlopen, build_opener, HTTPCookieProcessor
from http.cookiejar import CookieJar
import json
import re
import zipfile

ROOT = Path(__file__).resolve().parent
SOURCES = ROOT / "sources"
SOURCES.mkdir(parents=True, exist_ok=True)
UA = {"User-Agent": "Mozilla/5.0 (PH game audio asset preparation)"}


def get(url):
    with urlopen(Request(url, headers=UA), timeout=60) as response:
        return response.read()


def fetch(pack, page, author, files=None):
    folder = SOURCES / pack
    folder.mkdir(exist_ok=True)
    snapshot = folder / "source-page.html"
    if not snapshot.exists():
        snapshot.write_bytes(get(page))
    html = snapshot.read_text(encoding="utf-8")
    assert "CC0" in html or "publicdomain/zero" in html, page
    if files is None:
        download_id = re.search(r"data-download-id=['\"]([^'\"]+)", html).group(1)
        archive = folder / (pack + ".zip")
        if not archive.exists():
            opener = build_opener(HTTPCookieProcessor(CookieJar()))
            opener.open(Request(page, headers=UA), timeout=60).read()
            headers = {**UA, "Referer": page, "Accept": "application/json"}
            link = json.loads(opener.open(Request("https://kenney.nl/download-link/" + download_id, headers=headers), timeout=60).read())
            url = link.get("url") or link.get("download")
            if not url:
                raise ValueError(f"Unexpected link response keys: {list(link)}")
            archive.write_bytes(opener.open(Request(url, headers={**UA, "Referer": page}), timeout=60).read())
        with zipfile.ZipFile(archive) as bundle:
            # Only retain audio and license text; never extract scripts or paths.
            for member in bundle.infolist():
                suffix = Path(member.filename).suffix.lower()
                if suffix in (".wav", ".ogg") or Path(member.filename).name.lower() == "license.txt":
                    destination = folder / Path(member.filename).name
                    destination.write_bytes(bundle.read(member))
        files = [(p.name, page) for p in sorted(folder.iterdir()) if p.suffix in (".ogg", ".wav")]
    else:
        for filename, url in files:
            target = folder / filename
            if not target.exists():
                target.write_bytes(get(url))
    manifest = {
        "pack": pack, "author": author, "source_page": page,
        "license": "CC0-1.0", "license_url": "https://creativecommons.org/publicdomain/zero/1.0/",
        "retrieved_date": "2026-10-31",
        "files": [{"filename": filename, "download_or_pack_url": url,
                   "sha256": sha256((folder / filename).read_bytes()).hexdigest()}
                  for filename, url in files],
    }
    (folder / "source.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"{pack}: {len(files)} audio files", flush=True)


if __name__ == "__main__":
    jobs = [
        ("kenney-rpg", "https://kenney.nl/assets/rpg-audio", "Kenney", None),
        ("kenney-impact", "https://kenney.nl/assets/impact-sounds", "Kenney", None),
        ("typewriter", "https://opengameart.org/content/typewriter-sounds", "Cassie-OrbitGames",
         [(f"typewriter{i}.wav", f"https://opengameart.org/sites/default/files/typewriter{i}.wav") for i in range(1, 9)]),
        ("rain-window", "https://opengameart.org/content/rain-on-window-loop", "alxl",
         [("rain_on_window_loop.wav", "https://opengameart.org/sites/default/files/rain_on_window_loop.wav")]),
        ("rain-outside", "https://opengameart.org/content/rain-ambient-not-loopable-2-versions-available", "Ove Melaa",
         [("rain_long.ogg", "https://opengameart.org/sites/default/files/Ove%20Melaa%20-%20Rainy%20%28NOT%20loopable%29%20Long%20Version.ogg")]),
    ]
    with ThreadPoolExecutor(max_workers=4) as pool:
        futures = [pool.submit(fetch, *job) for job in jobs]
        for future in futures:
            future.result()
