"""Value SHAPES (not values) of MSShift event pages: every letter becomes 'a', every digit
'9', runs collapsed (e.g. '99/99/9999', 'aa-9'). Shows date/number formats without content.
Labels are only printed from the fixed allow-list; unknown labels as <N chars>.

    python -I tools/msshift-export/shapes.py <events-file.json>
"""
import json
import re
import sys
from collections import Counter, defaultdict
from html.parser import HTMLParser

LABELS = {"Event", "Detail", "Location Type", "Location Detail", "Priority", "Requested By", "Requested Date",
          "Requested Time", "Assigned To", "Completed Date", "Completed Time", "Relates To", "Type", "Name",
          "Event Narrative", "Started Date", "Started Time", "Shift Reported", "Department"}


def shape(t):
    s = re.sub(r"[A-Za-z]+", "a", t)
    s = re.sub(r"[0-9]+", "9", s)
    return s[:30]


class Leaves(HTMLParser):
    def __init__(self):
        super().__init__()
        self.stack, self.out = [], []

    def handle_starttag(self, tag, attrs):
        if tag in ("td", "span"):
            self.stack.append([tag, f"{tag}.{dict(attrs).get('class') or '-'}", []])

    def handle_endtag(self, tag):
        for i in range(len(self.stack) - 1, -1, -1):
            if self.stack[i][0] == tag:
                _, kind, buf = self.stack.pop(i)
                text = " ".join("".join(buf).split())
                if text:
                    self.out.append((kind, text))
                return

    def handle_data(self, data):
        if self.stack:
            self.stack[-1][2].append(data)


def main(path):
    data = json.load(open(path, encoding="utf-8"))
    head, vals, rows = defaultdict(Counter), defaultdict(Counter), Counter()
    for r in data["records"]:
        if not r.get("html"):
            continue
        p = Leaves()
        p.feed(r["html"])
        labels = []
        for kind, text in p.out:
            if kind in ("td.menufontH30", "td.menufontH312", "span.style2BoldGreen", "td.HeaderResult"):
                head[kind][text if text in LABELS else shape(text)] += 1
            elif kind == "td.style2":
                labels.append(text if text in LABELS else f"<{len(text)} chars>")
            elif kind in ("td.style3", "span.style3") and labels:
                vals[labels.pop(0)][shape(text)] += 1
        if r.get("row"):
            q = Leaves()
            q.feed(r["row"])
            rows[" | ".join(shape(t) for _, t in q.out)[:120]] += 1
    for kind, c in head.items():
        print(kind, c.most_common(6))
    for label, c in vals.items():
        print(f"{label:<18}", c.most_common(6))
    print("list row shapes:")
    for s, n in rows.most_common(5):
        print(f"  {n:>5}  {s}")


if __name__ == "__main__":
    main(sys.argv[1])
