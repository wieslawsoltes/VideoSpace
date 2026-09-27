#!/usr/bin/env python3
"""Verify the deployed artifact's exact source commit, with bounded CDN propagation retries."""
import json
import sys
import time
from urllib.parse import urljoin, urlparse
from urllib.request import Request, urlopen

base, expected = sys.argv[1:3]
if urlparse(base).scheme != 'https':
    raise SystemExit('Public Pages verification requires HTTPS')
last_error = None
for attempt in range(45):
    try:
        url = urljoin(base.rstrip('/') + '/', 'build-info.json') + '?commit=' + expected
        with urlopen(Request(url, headers={'Cache-Control': 'no-cache'}), timeout=15) as response:
            info = json.load(response)
        if info.get('commit') == expected and info.get('host') == 'Uno WebAssembly':
            print('Verified public Uno artifact:', json.dumps(info))
            break
        last_error = 'Public artifact does not yet match ' + expected + ': ' + repr(info)
    except Exception as error:
        last_error = str(error)
    time.sleep(2)
else:
    raise SystemExit('Pages verification failed: ' + str(last_error))
