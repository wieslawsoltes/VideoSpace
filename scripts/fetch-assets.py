#!/usr/bin/env python3
"""Fetch and verify the application font against reviewed upstream content hashes."""
from pathlib import Path
from urllib.request import urlopen
import hashlib
root = Path(__file__).resolve().parents[1] / 'src/VideoSpace.App/Assets/Fonts'
root.mkdir(parents=True, exist_ok=True)
files = {
    'Inter.ttf': ('https://raw.githubusercontent.com/google/fonts/main/ofl/inter/Inter%5Bopsz,wght%5D.ttf', '29160a80ff49ddcab2c97711247e08b1fab27a484a329ce8b813d820dc559031'),
    'OFL.txt': ('https://raw.githubusercontent.com/google/fonts/main/ofl/inter/OFL.txt', '5b9321a4298cfeb6b34354164a1c3afc3db114569984c502b9b35d988fd58c57')
}
for name, (url, expected) in files.items():
    target = root / name
    if not target.exists():
        with urlopen(url, timeout=60) as response: data = response.read(8 * 1024 * 1024)
        if hashlib.sha256(data).hexdigest() != expected: raise SystemExit('Upstream asset changed; review before updating its hash: ' + name)
        target.write_bytes(data)
    actual = hashlib.sha256(target.read_bytes()).hexdigest()
    if actual != expected: raise SystemExit('Unexpected local asset hash: ' + name)
    print(name, actual)
