"""Turns the MSShift event-log read-out (one file per month) into plain records for the
Shore Desk importer. Reads C:/DeskData/import/msshift/events/msshift-events-YYYY-MM.json and
writes C:/DeskData/import/msshift/parsed/events/events-YYYY-MM.json.

Event pages are tables: a row of labels (td.style2) and under it a row of values, grouped
under section headings (td.HeaderResult). The list row of the search result holds the
event number. Prints COUNTS ONLY, never values.

    python -I tools/msshift-import/parse_events.py
"""
import glob
import json
import os
import re
import sys
from collections import Counter
from datetime import datetime
from html.parser import HTMLParser

SRC = "C:/DeskData/import/msshift/events"
OUT = "C:/DeskData/import/msshift/parsed/events"
EMPTY = {"", "N/A", "n/a", "NA"}
LABELS = {"Location Type", "Location Detail", "Priority", "Requested Date", "Requested Time", "Assigned To",
          "Completed Date", "Completed Time", "Type", "Name", "Detail", "Started Date", "Started Time", "Shift Reported",
          "Entered by"}
SECTIONS = {"Event", "Assigned To", "Requested By", "Relates To", "Event Narrative"}


class Rows(HTMLParser):
    """Table rows as lists of (class, text) cells."""

    def __init__(self):
        super().__init__()
        self.rows, self.cell, self.row = [], None, None

    def handle_starttag(self, tag, attrs):
        if tag == "tr":
            self.row = []
        elif tag in ("td", "th") and self.row is not None:
            self.cell = [dict(attrs).get("class") or "-", []]

    def handle_endtag(self, tag):
        if tag in ("td", "th") and self.cell is not None and self.row is not None:
            text = " ".join("".join(self.cell[1]).split())
            if text:
                self.row.append((self.cell[0], text))
            self.cell = None
        elif tag == "tr" and self.row:
            self.rows.append(self.row)
            self.row = None

    def handle_data(self, data):
        if self.cell is not None:
            self.cell[1].append(data)


class Cells(HTMLParser):
    """Own text of each td in order (for the search-result list row)."""

    def __init__(self):
        super().__init__()
        self.cells, self.buf, self.depth = [], None, 0

    def handle_starttag(self, tag, attrs):
        if tag == "td":
            self.buf = []

    def handle_endtag(self, tag):
        if tag == "td" and self.buf is not None:
            self.cells.append(" ".join("".join(self.buf).split()))
            self.buf = None

    def handle_data(self, data):
        if self.buf is not None:
            self.buf.append(data)


DATE_FORMATS = ["%m/%d/%Y", "%m/%d/%y"]
TIME_FORMATS = ["%H:%M", "%I:%M %p", "%H:%M:%S"]


def when(date, time, bad):
    if not date or date in EMPTY:
        return None
    for df in DATE_FORMATS:
        try:
            d = datetime.strptime(date.strip(), df)
            break
        except ValueError:
            continue
    else:
        bad["date"] += 1
        return None
    if time and time not in EMPTY:
        for tf in TIME_FORMATS:
            try:
                t = datetime.strptime(time.strip(), tf)
                return d.replace(hour=t.hour, minute=t.minute).isoformat(timespec="minutes")
            except ValueError:
                continue
        bad["time"] += 1
    return d.isoformat(timespec="minutes")


def parse(rec, bad, unknown):
    p = Rows()
    p.feed(rec["html"])
    section, labels, f = None, None, {}
    for row in p.rows:
        if len(row) == 1 and row[0][0] == "HeaderResult" and row[0][1] in SECTIONS:
            section, labels = row[0][1], None
            continue
        if section is None:
            continue
        if all(c == "style2" for c, _ in row):
            labels = [t for _, t in row]
            continue
        if section == "Event Narrative":
            f.setdefault(("Event Narrative", "text"), " ".join(t for _, t in row))
            continue
        if labels:
            for i, (_, text) in enumerate(row):
                if i >= len(labels):
                    break
                label = labels[i]
                if label not in LABELS:
                    unknown[(section, i, len(label))] += 1
                    label = f"#{i}"
                f[(section, label)] = None if text in EMPTY else text
            labels = None
    g = lambda s, l: f.get((s, l))
    q = Cells()
    q.feed(rec.get("row") or "")
    cells = [c for c in q.cells if c]
    number = next((c for c in cells if re.match(r"^[A-Za-z]+\s*#\s*\d", c)), None)
    narrative = g("Event Narrative", "text")
    r = {
        "number": number,
        "category": g("Event", "#0"),
        "locationType": g("Event", "Location Type"),
        "locationDetail": g("Event", "Location Detail"),
        "shift": g("Event", "Shift Reported"),
        "priority": g("Event", "Priority"),
        "started": when(g("Assigned To", "Started Date"), g("Assigned To", "Started Time"), bad),
        "completed": when(g("Assigned To", "Completed Date"), g("Assigned To", "Completed Time"), bad),
        "enteredBy": g("Assigned To", "Entered by"),
        "assignedTo": g("Assigned To", "Assigned To"),
        "requested": when(g("Requested By", "Requested Date"), g("Requested By", "Requested Time"), bad),
        "requestedByName": g("Requested By", "Name"),
        "requestedByType": g("Requested By", "Type"),
        "requestedByDetail": g("Requested By", "Detail"),
        "relatesToName": g("Relates To", "Name"),
        "relatesToType": g("Relates To", "Type"),
        "relatesToDetail": g("Relates To", "Detail"),
        "narrative": None if narrative in EMPTY else narrative,
    }
    return {k: v for k, v in r.items() if v}


def main():
    os.makedirs(OUT, exist_ok=True)
    total, filled, bad, unknown = 0, Counter(), Counter(), Counter()
    numbers = Counter()
    for path in sorted(glob.glob(f"{SRC}/msshift-events-*.json")):
        month = re.search(r"(\d{4}-\d{2})", path).group(1)
        data = json.load(open(path, encoding="utf-8"))
        out = []
        for rec in data["records"]:
            if not rec.get("html"):
                bad["no page"] += 1
                continue
            r = parse(rec, bad, unknown)
            out.append(r)
            filled.update(r.keys())
            numbers[r.get("number")] += 1
        json.dump(out, open(f"{OUT}/events-{month}.json", "w", encoding="utf-8"), ensure_ascii=False)
        total += len(out)
    print(f"parsed {total} events from {len(glob.glob(f'{SRC}/msshift-events-*.json'))} month files")
    print("filled: " + ", ".join(f"{k} {n}" for k, n in sorted(filled.items())))
    print(f"missing numbers: {numbers.get(None, 0)}, duplicate numbers: {sum(1 for k, n in numbers.items() if k and n > 1)}")
    print("problems: " + (", ".join(f"{k} {n}" for k, n in bad.items()) or "none"))
    print("unknown labels (section/position/length): " + ", ".join(f"{s}/{i}/{n}:{c}" for (s, i, n), c in unknown.items()))


if __name__ == "__main__":
    sys.exit(main())
