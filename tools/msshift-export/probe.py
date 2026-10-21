"""Checks guesses against MSShift read-out files: for each candidate text given on the
command line, how many pages have a cell with exactly that text, and how many contain
it anywhere. Prints only the candidates you typed and counts - never page content.

    python -I tools/msshift-export/probe.py <file.json> "Pending" "Closed" ...
"""
import json
import sys
from html.parser import HTMLParser


class Cells(HTMLParser):
    def __init__(self):
        super().__init__()
        self.stack, self.cells = [], set()

    def handle_starttag(self, tag, attrs):
        if tag in ("td", "th", "span", "a", "div", "b"):
            self.stack.append([tag, []])

    def handle_endtag(self, tag):
        for i in range(len(self.stack) - 1, -1, -1):
            if self.stack[i][0] == tag:
                _, buf = self.stack.pop(i)
                self.cells.add(" ".join("".join(buf).split()))
                return

    def handle_data(self, data):
        for item in self.stack:
            item[1].append(data)


def main(path, *candidates):
    data = json.load(open(path, encoding="utf-8"))
    recs = [r for r in data["records"] if r.get("html")]
    pages = []
    for r in recs:
        p = Cells()
        p.feed(r["html"])
        pages.append((p.cells, r["html"], r.get("row") or ""))
    print(f"{path}: {len(pages)} pages")
    print(f"  {'candidate':<40} {'cell':>5} {'page':>5} {'row':>5}")
    for c in candidates:
        cell = sum(1 for cells, _, _ in pages if c in cells)
        anywhere = sum(1 for _, html, _ in pages if c in html)
        row = sum(1 for _, _, row in pages if c in row)
        print(f"  {c:<40} {cell:>5} {anywhere:>5} {row:>5}")


if __name__ == "__main__":
    main(*sys.argv[1:])
