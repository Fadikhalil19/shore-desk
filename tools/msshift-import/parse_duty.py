"""Turns the MSShift On Duty and Call Out read-outs (list pages, one file per month) into plain
records for the Shore Desk importer. Reads C:/DeskData/import/msshift/{onduty,callout}/*.json and
writes C:/DeskData/import/msshift/parsed/onduty.json and callout.json.

Both are plain result tables, one row per entry, recognised by the entry number in the first
cell (On Duty "XX#123", Call Out "XX#26-123"). Prints COUNTS ONLY, never values.

    python -I tools/msshift-import/parse_duty.py
"""
import glob
import json
import os
import re
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(__file__))
from parse_events import Rows, when, EMPTY  # noqa: E402

SRC = "C:/DeskData/import/msshift"
OUT = "C:/DeskData/import/msshift/parsed"
SHIFTS = {"Morning", "Afternoon", "Overnight"}
ONDUTY_NO = re.compile(r"^[A-Za-z]+#\d+$")
CALLOUT_NO = re.compile(r"^[A-Za-z]+#\d+-\d+$")


def v(text):
    return None if text is None or text in EMPTY else text


def stamp(text, bad):
    if not v(text):
        return None
    date, _, time = text.partition(" ")
    return when(date, time, bad)


def rows_of(kind, pattern):
    for path in sorted(glob.glob(f"{SRC}/{kind}/{kind}-*.json")):
        p = Rows()
        p.feed(json.load(open(path, encoding="utf-8")).get("page") or "")
        for row in p.rows:
            cells = [t for _, t in row]
            if cells and pattern.match(cells[0]):
                yield cells


def main():
    os.makedirs(OUT, exist_ok=True)
    bad, shapes = Counter(), Counter()

    duty, seen = [], set()
    for c in rows_of("onduty", ONDUTY_NO):
        if len(c) < 8:
            shapes[f"onduty row with {len(c)} cells"] += 1
            continue
        if c[0] in seen:
            continue
        seen.add(c[0])
        # Number | Name | Shift | Radio | Post | Start | End | Entered by
        r = {"number": c[0], "name": v(c[1]), "shift": v(c[2]), "radio": v(c[3]), "post": v(c[4]),
             "start": stamp(c[5], bad), "end": stamp(c[6], bad), "enteredBy": v(c[7])}
        duty.append({k: x for k, x in r.items() if x})

    calls, seen = [], set()
    for c in rows_of("callout", CALLOUT_NO):
        if len(c) < 8:
            shapes[f"callout row with {len(c)} cells"] += 1
            continue
        if c[0] in seen:
            continue
        seen.add(c[0])
        # Number | Shift date | Shift | Name | Department | Reason | Called (date time) | Taken by
        r = {"number": c[0], "date": when(v(c[1]), None, bad), "shift": v(c[2]), "name": v(c[3]),
             "department": v(c[4]), "reason": v(c[5]), "called": stamp(c[6], bad), "takenBy": v(c[7])}
        calls.append({k: x for k, x in r.items() if x})

    json.dump(duty, open(f"{OUT}/onduty.json", "w", encoding="utf-8"), ensure_ascii=False)
    json.dump(calls, open(f"{OUT}/callout.json", "w", encoding="utf-8"), ensure_ascii=False)
    count = lambda rows: ", ".join(f"{k} {n}" for k, n in sorted(Counter(k for r in rows for k in r).items()))
    print(f"on duty: {len(duty)} entries; filled: {count(duty)}")
    print(f"  shifts: {dict(Counter(r.get('shift') if r.get('shift') in SHIFTS else 'other' for r in duty))}")
    print(f"call outs: {len(calls)} entries; filled: {count(calls)}")
    print(f"  shifts: {dict(Counter(r.get('shift') if r.get('shift') in SHIFTS else 'other' for r in calls))}")
    print("problems: " + (", ".join(f"{k} {n}" for k, n in (bad + shapes).items()) or "none"))


if __name__ == "__main__":
    sys.exit(main())
