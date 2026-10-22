"""Row skeleton of ONE MSShift page: each table row as its cells, a cell shown as its text
only if it is in the fixed LABELS allow-list, else as its shape (letters 'a', digits '9').
Never prints values.

    python -I tools/msshift-export/rows.py <file.json> <record index>
"""
import json
import re
import sys
from html.parser import HTMLParser

LABELS = {"Event", "Detail", "Location Type", "Location Detail", "Priority", "Requested By", "Requested Date",
          "Requested Time", "Assigned To", "Completed Date", "Completed Time", "Relates To", "Type", "Name",
          "Event Narrative", "Started Date", "Started Time", "Shift Reported", "Department", "N/A", "Morning",
          "Afternoon", "Overnight", "Low", "Medium", "High", "Highest", "Employee", "Guest", "Non Guest", "Visitor",
          "Completed", "Pending"}


def shape(t):
    return re.sub(r"[0-9]+", "9", re.sub(r"[A-Za-z]+", "a", t))[:30]


class Rows(HTMLParser):
    def __init__(self):
        super().__init__()
        self.rows, self.cell, self.row, self.depth = [], None, None, 0

    def handle_starttag(self, tag, attrs):
        if tag == "tr":
            self.row = []
        elif tag in ("td", "th") and self.row is not None:
            self.cell = [dict(attrs).get("class") or "-", []]

    def handle_endtag(self, tag):
        if tag in ("td", "th") and self.cell is not None and self.row is not None:
            text = " ".join("".join(self.cell[1]).split())
            if text:
                self.row.append(f"{self.cell[0]}:" + (text if text in LABELS else f"[{shape(text)}]"))
            self.cell = None
        elif tag == "tr" and self.row:
            self.rows.append(self.row)
            self.row = None

    def handle_data(self, data):
        if self.cell is not None:
            self.cell[1].append(data)


def main(path, idx):
    data = json.load(open(path, encoding="utf-8"))
    rec = [r for r in data["records"] if r.get("html")][int(idx)]
    p = Rows()
    p.feed(rec["html"])
    for row in p.rows:
        print("  " + " | ".join(row))


if __name__ == "__main__":
    main(*sys.argv[1:])
