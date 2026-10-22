"""Local receiver for MSShift incident photos and PDF attachments (phase 4 read-out).

Runs on this server only (127.0.0.1). The in-page script (files.js, in the signed-in MSShift
tab) asks it for the to-do list, fetches each file from MSShift (read-only) and posts it
here; files are written straight to C:/DeskData/import/msshift/files (locked folder), never
to Downloads. Only the MSShift origin may call it, and every call needs the run key.
Prints COUNTS ONLY.

    python -I tools/msshift-export/receiver.py            (prints the key to use in files.js)
"""
import glob
import json
import os
import re
import secrets
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

SRC = "C:/DeskData/import/msshift"
OUT = "C:/DeskData/import/msshift/files"
ORIGIN = "https://msshift-security-2.com"
PORT = 8765
KEY = secrets.token_urlsafe(16)
MAX_BYTES = 60 * 1024 * 1024
TYPES = {"image/jpeg": "jpg", "image/png": "png", "image/gif": "gif", "application/pdf": "pdf", "video/mp4": "mp4"}


sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "msshift-import"))
from parse_incidents import Leaves, field, fix_unknown_labels, sections_of  # noqa: E402


def report_number(html):
    p = Leaves()
    p.feed(html)
    n = field(fix_unknown_labels(sections_of(p.out)), "_head", "Report Number")
    return re.sub(r"[^A-Za-z0-9\-]", "", n) if n else None


def todo():
    """Photos and PDF attachments named in the saved incident pages."""
    items = []
    for path in sorted(glob.glob(f"{SRC}/msshift-incidents-*.json")):
        form = re.search(r"incidents-(\w+)\.json", path).group(1)
        data = json.load(open(path, encoding="utf-8"))
        for rec in data["records"]:
            html = rec.get("html") or ""
            number = report_number(html)
            if not number:
                continue
            for i, url in enumerate(dict.fromkeys(re.findall(r'src="https?://msshift-security-2\.com(/d/EventLog/uploadpic/[^"]+)"', html)), 1):
                items.append({"id": f"{form}-{number}-photo{i}", "kind": "photo", "url": url})
            for i, token in enumerate(dict.fromkeys(re.findall(r"View_PDF_2\('([^']+)'\)", html)), 1):
                items.append({"id": f"{form}-{number}-pdf{i}", "kind": "pdf", "url": f"/PDF_Viewer_2?FP={token}&H=800"})
    return items


def done_ids():
    return sorted({os.path.splitext(os.path.basename(p))[0] for p in glob.glob(f"{OUT}/*") if not p.endswith(".jsonl")})


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def cors(self):
        self.send_header("Access-Control-Allow-Origin", ORIGIN)
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type, X-Run-Key, X-Source-Type")
        self.send_header("Access-Control-Allow-Private-Network", "true")

    def reply(self, code, obj=None):
        body = json.dumps(obj if obj is not None else {}).encode()
        self.send_response(code)
        self.cors()
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def allowed(self):
        return self.headers.get("Origin") == ORIGIN and self.headers.get("X-Run-Key") == KEY

    def do_OPTIONS(self):
        self.send_response(204)
        self.cors()
        self.end_headers()

    def do_GET(self):
        if not self.allowed():
            return self.reply(403)
        if self.path == "/list":
            return self.reply(200, {"items": todo(), "done": done_ids()})
        return self.reply(404)

    def do_POST(self):
        if not self.allowed():
            return self.reply(403)
        m = re.fullmatch(r"/file/([A-Za-z]+-[A-Za-z0-9#\-]+-(?:photo|pdf)\d+)", self.path)
        size = int(self.headers.get("Content-Length") or 0)
        ctype = (self.headers.get("X-Source-Type") or "").split(";")[0].strip().lower()
        if not m or size <= 0 or size > MAX_BYTES or ctype not in TYPES:
            return self.reply(400, {"error": "bad request"})
        data = self.rfile.read(size)
        name = m.group(1).replace("#", "")
        with open(f"{OUT}/{name}.{TYPES[ctype]}", "wb") as f:
            f.write(data)
        with open(f"{OUT}/manifest.jsonl", "a", encoding="utf-8") as f:
            f.write(json.dumps({"id": name, "type": ctype, "bytes": size}) + "\n")
        Handler.saved += 1
        if Handler.saved % 100 == 0:
            print(f"saved {Handler.saved} files", flush=True)
        return self.reply(200, {"ok": True})

    saved = 0


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    items = todo()
    print(f"to do: {sum(1 for i in items if i['kind'] == 'photo')} photos, {sum(1 for i in items if i['kind'] == 'pdf')} PDFs; "
          f"already saved {len(done_ids())}", flush=True)
    print(f"key {KEY}", flush=True)
    ThreadingHTTPServer(("127.0.0.1", PORT), Handler).serve_forever()
