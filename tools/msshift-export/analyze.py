"""Structure survey of MSShift read-out files: which field labels the report pages
have and how often each is filled in. Prints LABELS AND COUNTS ONLY, never values,
because the files hold personal and incident data.

    python -I tools/msshift-export/analyze.py C:/DeskData/import/msshift/msshift-incidents-IR.json
"""
import json
import sys
from collections import Counter
from html.parser import HTMLParser


class Cells(HTMLParser):
    """Collects the text of each table cell, in page order."""

    def __init__(self):
        super().__init__()
        self.cells, self.depth, self.buf = [], 0, []

    def handle_starttag(self, tag, attrs):
        if tag in ("td", "th"):
            if self.depth and self.buf:
                self.cells.append(" ".join("".join(self.buf).split()))
            self.depth += 1
            self.buf = []

    def handle_endtag(self, tag):
        if tag in ("td", "th") and self.depth:
            self.cells.append(" ".join("".join(self.buf).split()))
            self.buf = []
            self.depth -= 1

    def handle_data(self, data):
        if self.depth:
            self.buf.append(data)


def is_label(text):
    return 2 < len(text) < 45 and (text.endswith(":") or text.endswith("*")) and not any(ch.isdigit() for ch in text[:3])


def main(path):
    data = json.load(open(path, encoding="utf-8"))
    records = [r for r in data["records"] if r.get("html")]
    pages = []
    for r in records:
        p = Cells()
        p.feed(r["html"])
        pages.append([c for c in p.cells if c])
    # A label is short text found on (nearly) every page; values vary from page to page.
    on = Counter(c for cells in pages for c in set(cells) if len(c) < 45)
    labels = {c for c, n in on.items() if n >= 0.8 * len(pages)}
    filled = Counter()
    for cells in pages:
        for i, c in enumerate(cells[:-1]):
            if c in labels and cells[i + 1] not in labels:
                filled[c] += 1
    print(f"{path}: {len(records)} records, {len(labels)} labels")
    first = pages[0]
    for c in sorted(labels, key=lambda x: first.index(x) if x in first else 9999):
        print(f"  {c:<45} filled {filled[c]:>4} of {len(pages)}")


if __name__ == "__main__":
    main(sys.argv[1])
