"""Turns the MSShift lost & found read-out (found / returned items, one file per month) into
plain records for the Shore Desk importer. Reads C:/DeskData/import/msshift/found/found-YYYY-MM.json
and writes C:/DeskData/import/msshift/parsed/found.json.

Item pages are tables like the event pages: a row of labels and under it a row of values,
under section headings (td.HeaderResult). Labels are recognised only from the fixed LABELS
list (staff names share the label class). The page title holds the item number. Prints
COUNTS ONLY, never values.

    python -I tools/msshift-import/parse_found.py
"""
import glob
import json
import os
import re
import sys
from collections import Counter
from datetime import datetime

sys.path.insert(0, os.path.dirname(__file__))
from parse_events import Rows, when, EMPTY  # noqa: E402

SRC = "C:/DeskData/import/msshift/found"
OUT = "C:/DeskData/import/msshift/parsed"
LABELS = {
    "Item Category", "Item Detail", "Color", "Brand / Model / Serial", "Item Description", "Date & Time Found",
    "Purge Date", "Location Found", "Location Detail", "Registered Guest Name", "Found By", "Found By | Wants Item?",
    "Detail", "Received By", "Department", "Entered By", "Storage Location", "Storage Location / Purge Date",
    "Date Received", "Time Received", "Found Narrative", "Return To", "Disposition", "Type", "Room #", "Phone #",
    "Arrival, Departure", "Email", "Date Returned", "Time Returned", "Returned By", "Return Narrative", "Carrier",
    "Shipped By", "Date & Time Shipped", "Address", "City", "State", "Country", "Date", "Taken By", "Location",
}
NUMBER = re.compile(r"\b(\d{2}-\d+)\s*$")
CARD = re.compile(r"(?:\d[ -]?){13,19}")
STAMP = re.compile(r"^\d{1,2}/\d{1,2}/\d{4} \d{1,2}:\d{2}$")


def label_row(row):
    """Label texts of a label row; a label not in LABELS becomes ?<length> (its text is never kept)."""
    cells = [(c, t.rstrip(":").strip()) for c, t in row if not c.startswith("TopModule")]
    # Pages of items still in storage label two rows in a plain cell beside an Edit button
    # (found date + purge date, storage area): ?10 and ?12.
    if len(cells) == 1 and len(cells) < len(row) and cells[0][0] == "-":
        return [f"?{len(cells[0][1])}"]
    if not cells or not any(t in LABELS for _, t in cells):
        return None
    if not all(t in LABELS or c == "style2" for c, t in cells):
        return None
    return [t if t in LABELS else f"?{len(t)}" for _, t in cells]


def parse(rec, unknown, shapes):
    p = Rows()
    p.feed(rec["html"])
    f, seen, number, site = {}, Counter(), None, None
    labels, pending = None, None
    for row in p.rows:
        texts = [t for _, t in row]
        if number is None and len(row) == 1 and row[0][0] in ("h2", "menufontH312"):
            m = NUMBER.search(row[0][1])
            number = m.group(1) if m else None
            continue
        if site is None and len(row) == 1 and row[0][0] == "style2" and re.match(r"^\d+ ", row[0][1]):
            site = row[0][1]
            continue
        if row[0][0] == "HeaderResult":
            labels = None
            if texts[0] == "Pictures":
                break
            continue
        lab = label_row(row)
        if lab:
            labels = [t.rstrip(":").strip() for t in lab]
            # A label that is used twice (Department) gets a running number: Department, Department 2, ...
            for i, l in enumerate(labels):
                seen[l] += 1
                if seen[l] > 1:
                    labels[i] = f"{l} {seen[l]}"
            continue
        # Items still in storage show the found time under a heading of its own, without a label.
        if "found" not in f and row[0][0] == "style3" and STAMP.match(row[0][1]):
            f["found"] = row[0][1]
        if labels:
            vals = [t for c, t in row if c in ("style3", "-")]
            for i, l in enumerate(labels):
                if i < len(vals):
                    f[l] = None if vals[i] in EMPTY else vals[i]
            if labels == ["?10"] and len(vals) > 1:       # found date row: the purge date sits beside it
                if not f.get("Purge Date") and vals[1] not in EMPTY:
                    f["Purge Date"] = vals[1]
            # A narrative label is followed by one value cell; anything else under it is a second line.
            pending, labels = (labels[-1] if len(labels) == 1 else None), None
            continue
        if pending and texts:
            unknown[("after " + pending, len(row))] += 1
        elif all(c == "style2" for c, _ in row) and len(row) <= 2:
            for _, t in row:
                unknown[("label", len(t))] += 1
    return number, site, f


def split2(v, sep):
    if not v:
        return None, None
    a, _, b = v.partition(sep)
    a, b = a.strip(), b.strip()
    return (None if a in EMPTY else a or None), (None if b in EMPTY else b or None)


def to_record(number, site, f, bad):
    found_date, found_time = split2(f.get("Date & Time Found") or f.get("?10") or f.get("found"), " ")
    by_type, wants = split2(f.get("Found By | Wants Item?"), "|")
    storage, purge = f.get("Storage Location"), f.get("Purge Date")
    if f.get("?12"):                          # the storage area beside (or instead of) Storage Location
        storage = f"{f['?12']} / {storage}" if storage else f["?12"]
    arrival, departure = split2(f.get("Arrival, Departure"), "-")
    r = {
        "number": number,
        "site": site,
        "category": f.get("Item Category"),
        "detail": f.get("Item Detail"),
        "color": f.get("Color"),
        "brandModel": f.get("Brand / Model / Serial"),
        "description": f.get("Item Description"),
        "found": when(found_date, found_time, bad),
        "purge": when(*split2(purge, " "), bad) if purge and re.match(r"^\d", purge) else None,
        "locationFound": f.get("Location Found"),
        "locationDetail": f.get("Location Detail"),
        "registeredGuest": f.get("Registered Guest Name"),
        "foundByName": by_type or f.get("Found By"),
        "foundByDepartment": f.get("Detail"),
        "wantsItem": wants,
        "received": when(f.get("Date Received"), f.get("Time Received"), bad),
        "receivedBy": f.get("Received By"),
        "receivedByDepartment": f.get("Department"),
        "enteredBy": f.get("Entered By"),
        "storage": storage,
        "foundNarrative": f.get("Found Narrative"),
        "returnTo": f.get("Return To"),
        "disposition": f.get("Disposition"),
        "returnToType": f.get("Type"),
        "room": f.get("Room #"),
        "returnToDepartment": f.get("Department 2"),
        "phone": f.get("Phone #"),
        "arrival": when(arrival, None, bad) if arrival else None,
        "departure": when(departure, None, bad) if departure else None,
        "email": f.get("Email"),
        "returned": when(f.get("Date Returned"), f.get("Time Returned"), bad),
        "returnedBy": f.get("Returned By"),
        "returnedByDepartment": f.get("Department 3"),
        "returnNarrative": f.get("Return Narrative"),
        "carrier": f.get("Carrier"),
        "shippedBy": f.get("Shipped By"),
        "shipped": when(*split2(f.get("Date & Time Shipped"), " "), bad) if f.get("Date & Time Shipped") else None,
        "shipAddress": f.get("Address"),
        "shipCity": f.get("City"),
        "shipState": f.get("State"),
        "shipCountry": f.get("Country"),
    }
    # Card numbers must never come across (LostFoundRules.LooksLikeCardNumber).
    for k, v in r.items():
        if isinstance(v, str) and k not in ("found", "purge", "received", "returned", "shipped", "arrival", "departure") and CARD.search(v):
            r[k] = CARD.sub("[number removed]", v)
            bad["card-like number removed"] += 1
    return {k: v for k, v in r.items() if v}


def main():
    os.makedirs(OUT, exist_ok=True)
    out, filled, bad, unknown, shapes = [], Counter(), Counter(), Counter(), Counter()
    numbers, sites = Counter(), Counter()
    files = sorted(glob.glob(f"{SRC}/found-*.json"))
    for path in files:
        data = json.load(open(path, encoding="utf-8"))
        for rec in data["records"]:
            if not rec.get("html"):
                bad["no page"] += 1
                continue
            number, site, f = parse(rec, unknown, shapes)
            r = to_record(number, site, f, bad)
            numbers[number] += 1
            sites[site] += 1
            if number and numbers[number] > 1:
                continue                      # the same item listed twice (found + returned row)
            out.append(r)
            filled.update(r.keys())
    json.dump(out, open(f"{OUT}/found.json", "w", encoding="utf-8"), ensure_ascii=False)
    print(f"parsed {len(out)} found items from {len(files)} month files")
    print("filled: " + ", ".join(f"{k} {n}" for k, n in sorted(filled.items())))
    print(f"missing numbers: {numbers.get(None, 0)}, listed twice: {sum(1 for k, n in numbers.items() if k and n > 1)}")
    print(f"sites: {len(sites)} different, starting 1515: {sum(n for k, n in sites.items() if (k or '').startswith('1515 '))}")
    print("problems: " + (", ".join(f"{k} {n}" for k, n in bad.items()) or "none"))
    print("unknown rows: " + ", ".join(f"{k}:{n}" for k, n in unknown.most_common(15)))


if __name__ == "__main__":
    sys.exit(main())
