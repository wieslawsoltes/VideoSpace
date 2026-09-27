#!/usr/bin/env python3
"""Fetch the application font from its upstream permissively licensed source."""
from pathlib import Path
from urllib.request import urlopen
import hashlib
root = Path(__file__).resolve().parents[1] / 'src/VideoSpace.App/Assets/Fonts'
root.mkdir(parents=True, exist_ok=True)
files = {'Inter.ttf': 'https://raw.githubusercontent.com/google/fonts/main/ofl/inter/Inter%5Bopsz,wght%5D.ttf', 'OFL.txt': 'https://raw.githubusercontent.com/google/fonts/main/ofl/inter/OFL.txt'}
for name, url in files.items():
    target = root / name
    if not target.exists():
        with urlopen(url, timeout=60) as response:
            data = response.read(8 * 1024 * 1024)
        if len(data) < 100: raise SystemExit('Font asset was empty: ' + url)
        target.write_bytes(data)
    print(name, hashlib.sha256(target.read_bytes()).hexdigest())
