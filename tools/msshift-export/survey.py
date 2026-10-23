"""Structure survey of a module file saved by modules.js (lost & found, on duty, call outs).
Prints tag.class names, counts and value SHAPES (letters -> a, digits -> 9) only. Text is
printed only when it is in the fixed GUESSES allow-list below, so names and record content
never reach the screen.

    python -I tools/msshift-export/survey.py <module-file.json> [page | record index]
"""
import json
import re
import sys
from collections import Counter, defaultdict
from html.parser import HTMLParser

# Field names guessed from the MSShift walkthrough (docs/msshift-notes.md). Only these print.
GUESSES = {
    "Found Date", "Found Time", "Found Date & Time", "Date Found", "Time Found", "Category", "Detail", "Item",
    "Description", "Item Description", "Location Found", "Found Location", "Location", "Registered Guest", "Guest",
    "Room", "Room Number", "Guest Room Number", "Color", "Primary Color", "Brand", "Model / Serial", "Model/Serial",
    "Serial", "Found By", "Found By Type", "Type", "Department", "Associate", "Wants Item", "Wants Item ?",
    "Wants Item?", "Received By", "Stored In", "Location Stored", "Storage", "Storage Location", "Purge Date",
    "Narrative", "Notes", "Note", "Comments", "Found Narrative", "Status", "Returned", "Return Date", "Returned To",
    "Returned By", "Return Date & Time", "Returned Date", "Returned Time", "Return Narrative", "Disposition",
    "Disposed", "Disposal", "Disposal Date", "Shipped", "Ship", "Shipping", "Tracking", "Tracking #", "Carrier",
    "Found #", "Found Number", "Item Number", "Item #", "Log #", "Number", "Hotel", "Property", "Value",
    "Quantity", "Qty", "Name", "Phone", "Email", "Address", "Date", "Time", "Shift", "Shift Reported", "Officer",
    "Employee", "Employee Name", "Radio", "Radio #", "Post", "Start", "End", "Start Time", "End Time", "Reason",
    "Call Out", "Called Out", "Called By", "Taken By", "Entered By", "Reported By", "Date & Time", "Yes", "No",
    "N/A", "X", "Total", "Lost", "Found", "Return", "Search", "Print", "Close",
}


def shape(t):
    return re.sub(r"[0-9]+", "9", re.sub(r"[A-Za-z]+", "a", t))[:30]


class Leaves(HTMLParser):
    """(tag.class, text) for each element with its own text, in page order."""

    def __init__(self):
        super().__init__()
        self.stack, self.out = [], []

    def handle_starttag(self, tag, attrs):
        if tag in ("td", "th", "span", "a", "b", "div", "label"):
            self.stack.append([f"{tag}.{dict(attrs).get('class') or '-'}", tag, []])

    def handle_endtag(self, tag):
        for i in range(len(self.stack) - 1, -1, -1):
            if self.stack[i][1] == tag:
                kind, _, buf = self.stack.pop(i)
                text = " ".join("".join(buf).split()).rstrip(":").strip()
                if text:
                    self.out.append((kind, text))
                return

    def handle_data(self, data):
        if self.stack:
            self.stack[-1][2].append(data)


def leaves(html):
    p = Leaves()
    p.feed(html or "")
    return p.out


def show(text):
    return repr(text) if text in GUESSES else f"<{len(text)} chars, {shape(text)}>"


def main():
    data = json.load(open(sys.argv[1], encoding="utf-8"))
    which = sys.argv[2] if len(sys.argv) > 2 else None
    recs = [r for r in data.get("records", []) if r.get("html")]
    print(f"kind {data.get('kind')} month {data.get('month')} listed {data.get('listed')} records {len(recs)}")
    if which is None:
        # Which classes look like labels: few distinct texts repeated on many records.
        texts = defaultdict(Counter)
        for r in recs:
            for kind, t in leaves(r["html"]):
                texts[kind][t] += 1
        for kind, c in sorted(texts.items(), key=lambda kv: -sum(kv[1].values())):
            known = sum(1 for t in c if t in GUESSES)
            print(f"{kind:40} leaves {sum(c.values()):6}  distinct {len(c):5}  guessed labels {known}")
        return
    html = data.get("page") if which == "page" else recs[int(which)]["html"]
    for kind, t in leaves(html)[:400]:
        print(f"{kind:32} {show(t)}")


if __name__ == "__main__":
    main()
