#!/usr/bin/env python3
"""Collect the actual Uno publish root without changing bootstrapper resource paths."""
import argparse
import json
import os
import shutil
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('publish', type=Path)
parser.add_argument('output', type=Path)
args = parser.parse_args()
candidates = sorted(args.publish.rglob('index.html'), key=lambda p: len(p.parts))
if not candidates:
    raise SystemExit('Uno publish did not produce index.html')
source = candidates[0].parent
args.output.mkdir(parents=True, exist_ok=True)
shutil.copytree(source, args.output, dirs_exist_ok=True)
(args.output / '.nojekyll').touch()
version = os.environ.get('VERSION') or ET.parse(Path(__file__).resolve().parents[1] / 'Directory.Build.props').find('.//Version').text
(args.output / 'build-info.json').write_text(json.dumps({
    'application': 'VideoSpace', 'host': 'Uno WebAssembly', 'version': version,
    'commit': os.environ.get('GITHUB_SHA', 'local'), 'builtAt': datetime.now(timezone.utc).isoformat()
}), encoding='utf-8')
print('Collected', source, 'into', args.output)
