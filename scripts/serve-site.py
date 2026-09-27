#!/usr/bin/env python3
"""Local server matching the Pages project base path. No cross-origin isolation is required."""
import argparse, http.server, functools
from urllib.parse import urlsplit
parser = argparse.ArgumentParser(); parser.add_argument('--directory', default='artifacts/site'); parser.add_argument('--port', type=int, default=4173)
args = parser.parse_args()
class Handler(http.server.SimpleHTTPRequestHandler):
    extensions_map = {**http.server.SimpleHTTPRequestHandler.extensions_map, '.wasm': 'application/wasm', '.js': 'text/javascript', '.json': 'application/json', '.webmanifest': 'application/manifest+json'}
    def translate_path(self, path):
        path = urlsplit(path).path
        if path.startswith('/VideoSpace/'): path = path[len('/VideoSpace'):]
        return super().translate_path(path)
    def end_headers(self):
        self.send_header('Cache-Control', 'no-cache')
        super().end_headers()
http.server.ThreadingHTTPServer(('0.0.0.0', args.port), functools.partial(Handler, directory=args.directory)).serve_forever()
