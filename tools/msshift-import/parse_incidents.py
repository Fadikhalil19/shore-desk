"""Turns the MSShift incident read-out (report view pages) into plain records for the
Shore Desk importer. Reads C:/DeskData/import/msshift/msshift-incidents-<form>.json and
writes C:/DeskData/import/msshift/parsed/incidents.json.

Prints COUNTS ONLY (how many reports got each field), never values: the pages hold
injuries, birth dates and addresses. Unknown labels are shown as <section>/<N chars>.

    python -I tools/msshift-import/parse_incidents.py
"""
import json
import os
import re
import sys
from collections import Counter
from datetime import datetime
from html.parser import HTMLParser

SRC = "C:/DeskData/import/msshift"
OUT = "C:/DeskData/import/msshift/parsed"
FORMS = ["IR", "GAI", "EAI", "MD", "HI"]

SECTIONS = {
    "Subject", "Subject Address", "Victim", "Victim Address", "Injury Type", "Incident Narrative - Statements of Facts",
    "Investigation/Follow Up", "Emergency Medical Service Required (EMS) , Refused Medical Attention (RMA)",
    "Police Involvement", "Contributing Factors, Symptoms", "Witnesses", "Does Incident involve a Vehicle ?",
    "Lock Interrogation", "Pictures / Video Taken ?", "Was Evidence Collected ?", "Report Distributed To",
    "Report Incident to Claims", "Reporting Party", "Hotel Property Involved ?", "Total Claim Value",
    "Property Missing/Damaged", "Pictures", "PDF Attachments",
}
HEADING_KINDS = {"span.style2Bold", "td.style2Bold"}
VALUE_KINDS = {"span.style15", "span.style24", "td.style15", "span.Style15"}
STATUSES = {"Closed", "Pending", "Locked", "Deleted"}

LABELS = {
    "Report Number", "Incident Date & Time", "Incident Type", "Reported Date & Time", "Incident Location",
    "Written Date & Time", "Injury Type", "Body Part", "Body Part Location", "Subject", "Employee Name",
    "Date of Birth", "Department", "Age", "Position", "Employee Number", "Date of Hire", "Home Street",
    "Work Street", "Home City", "Work City", "Home State", "Work State", "Home Zip", "Work Zip", "Home Country",
    "Work Country", "Home Phone", "Work Phone", "EMS", "RMA", "Respond Date & Time", "EMS Name", "EMS #",
    "Hospital Name", "Contributing Factors", "Consumed Date & Time", "Witness Name", "Witness 2 Name",
    "Witness Phone", "Witness 2 Phone", "Witness Address", "Witness 2 Address", "Vehicle Involved ?",
    "Circumstances", "Vehicle Operator", "Operator Name", "Operator DOB", "Operator D.L", "Operator Phone",
    "Vehicle Owner Name", "Vehicle Owner Address", "Vehicle Owner Phone", "Vehicle Make", "Vehicle Model",
    "Vehicle Year", "Vehicle Color", "License Plate/State", "VIN #", "Insurance Company", "Rental Company",
    "Pictures Taken ?", "Video Taken ?", "Pictures Date & Time", "Video Date & Time", "Number of Pictures",
    "Video Description", "Pictures Taken By", "Item Description", "Date & Time", "Location Stored", "Disposition",
    "Reported By", "Written By", "Reviewed By", "Subject Name", "Guest Room Number", "Room Number", "Subject Type",
    "Arrival - Departure", "Gender", "Group/Company", "Email", "Victim Name", "Victim Type", "Height, Weight",
    "Eyes , Hair", "Type Detail", "D.L #, D.L State", "Race", "Scars/Marks/Tattoos", "Date of Birth, Age",
    "Officer Name", "Police Case #", "Total Claim Value",
}
EMPTY = {"", "N/A", "n/a", "NA"}


class Leaves(HTMLParser):
    """Own text of each td/span/a, in page order, as (kind, text)."""

    def __init__(self):
        super().__init__()
        self.stack, self.out, self.links = [], [], []

    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if tag in ("td", "th", "span", "a", "div", "b"):
            self.stack.append([tag, f"{tag}.{a.get('class') or '-'}", []])
        if tag == "a" and a.get("href"):
            self.links.append(a["href"])

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


def sections_of(leaves):
    """{section: [(label or None, [values])]} - None label = free text in the section."""
    secs = {"_head": []}
    cur = "_head"
    pending = None
    for kind, text in leaves:
        if kind in HEADING_KINDS and text in SECTIONS:
            cur = text
            secs.setdefault(cur, [])
            pending = None
            continue
        if text in LABELS and kind not in VALUE_KINDS - {"td.style15"}:
            pending = [text, []]
            secs[cur].append(pending)
            continue
        if pending is not None and kind in VALUE_KINDS:
            pending[1].append(text)
            continue
        # An unknown label: short td text directly followed by a value span.
        pending = None
        secs[cur].append([None, [text], kind])
    return secs


def fix_unknown_labels(secs):
    """Second pass: a free cell followed by a value-only row becomes '?<text>' label."""
    for name, items in secs.items():
        out = []
        for item in items:
            if item[0] is None and len(item) == 3 and item[2] in ("td.-", "td.style22", "td.style15") and len(item[1][0]) <= 30:
                out.append(["?" + item[1][0], [], item[2]])
            elif item[0] is None and out and out[-1][0] and out[-1][0].startswith("?") and not out[-1][1] and len(item) == 3 and item[2] in VALUE_KINDS:
                out[-1][1].append(item[1][0])
            else:
                out.append(item)
        secs[name] = out
    return secs


def val(values):
    vals = [v for v in values if v not in EMPTY]
    return " ".join(vals) if vals else None


def field(secs, section, label, idx=None):
    for item in secs.get(section, []):
        if item[0] == label:
            if idx is None:
                return val(item[1])
            vals = item[1]
            return vals[idx] if len(vals) > idx and vals[idx] not in EMPTY else None
    return None


def free(secs, section, min_len=0):
    texts = [item[1][0] for item in secs.get(section, []) if (item[0] is None or item[0].startswith("?")) and item[1]
             and item[1][0] not in EMPTY and len(item[1][0]) >= min_len]
    return "\n".join(texts) if texts else None


def unknown(secs, section):
    return [(item[0][1:], val(item[1])) for item in secs.get(section, []) if item[0] and item[0].startswith("?")]


DATE_FORMATS = ["%m/%d/%Y %H:%M", "%m/%d/%Y %I:%M %p", "%m/%d/%Y %H:%M:%S", "%m/%d/%Y", "%m-%d-%Y %H:%M"]


def when(text, bad):
    if not text:
        return None
    t = text.strip()
    for fmt in DATE_FORMATS:
        try:
            return datetime.strptime(t, fmt).isoformat(timespec="minutes")
        except ValueError:
            pass
    bad[len(t)] += 1
    return None


def address(secs, section, kind):
    a = {k: field(secs, section, f"{kind} {k}") for k in ("Street", "City", "State", "Zip", "Country")}
    return a if any(a.values()) else None


def person(secs, form, section):
    """The main subject (or victim for High Level)."""
    p = {}
    if form == "EAI":
        p.update(name=field(secs, section, "Employee Name"), type="Employee", department=field(secs, section, "Department"),
                 position=field(secs, section, "Position"), employeeNumber=field(secs, section, "Employee Number"),
                 dateOfBirth=field(secs, section, "Date of Birth"), dateOfHire=field(secs, section, "Date of Hire"))
    elif form == "HI":
        key = "Victim" if section == "Victim" else "Subject"
        p.update(name=field(secs, section, f"{key} Name"), type=field(secs, section, f"{key} Type"),
                 typeDetail=field(secs, section, "Type Detail"), gender=field(secs, section, "Gender"),
                 race=field(secs, section, "Race"), email=field(secs, section, "Email"),
                 height=field(secs, section, "Height, Weight", 0), weight=field(secs, section, "Height, Weight", 1),
                 eyes=field(secs, section, "Eyes , Hair", 0), hair=field(secs, section, "Eyes , Hair", 1),
                 license=field(secs, section, "D.L #, D.L State", 0), licenseState=field(secs, section, "D.L #, D.L State", 1),
                 marks=field(secs, section, "Scars/Marks/Tattoos"), dateOfBirth=field(secs, section, "Date of Birth, Age", 0),
                 extra=unknown(secs, section))
    else:
        p.update(name=field(secs, section, "Subject Name") or field(secs, section, "Subject"),
                 type=field(secs, section, "Subject Type"),
                 room=field(secs, section, "Guest Room Number") or field(secs, section, "Room Number"),
                 stay=field(secs, section, "Arrival - Departure"), gender=field(secs, section, "Gender"),
                 company=field(secs, section, "Group/Company"), dateOfBirth=field(secs, section, "Date of Birth"),
                 email=field(secs, section, "Email"))
    addr = section + " Address"
    p["home"] = address(secs, addr, "Home")
    p["work"] = address(secs, addr, "Work")
    p["homePhone"] = field(secs, addr, "Home Phone")
    p["workPhone"] = field(secs, addr, "Work Phone")
    p = {k: v for k, v in p.items() if v}
    return p or None


def parse(form, html, bad_dates):
    lv = Leaves()
    lv.feed(html)
    leaves = lv.out
    secs = fix_unknown_labels(sections_of(leaves))
    head = secs["_head"]
    status = next((t for k, t in leaves if t in STATUSES), None)
    number = field(secs, "_head", "Report Number")
    location = field(secs, "_head", "Incident Location")
    r = {
        "form": form,
        "number": number,
        "status": status,
        "type": field(secs, "_head", "Incident Type"),
        "occurred": when(field(secs, "_head", "Incident Date & Time"), bad_dates),
        "reported": when(field(secs, "_head", "Reported Date & Time"), bad_dates),
        "written": when(field(secs, "_head", "Written Date & Time"), bad_dates),
        "location": location,
        "narrative": free(secs, "Incident Narrative - Statements of Facts"),
        "followUp": free(secs, "Investigation/Follow Up") or val([v for i in secs.get("Investigation/Follow Up", []) for v in i[1]]),
        "distributedTo": free(secs, "Report Distributed To"),
        "claims": free(secs, "Report Incident to Claims"),
        "lockInterrogation": free(secs, "Lock Interrogation"),
        "reportedBy": field(secs, "Reporting Party", "Reported By"),
        "writtenBy": field(secs, "Reporting Party", "Written By", 0),
        "reviewedBy": field(secs, "Reporting Party", "Reviewed By", 0),
    }
    if form in ("GAI", "EAI"):
        r["injury"] = {k: v for k, v in {
            "injuryType": field(secs, "Injury Type", "Injury Type") or field(secs, "_head", "Injury Type"),
            "bodyPart": field(secs, "Injury Type", "Body Part") or field(secs, "_head", "Body Part"),
            "bodyPartLocation": field(secs, "Injury Type", "Body Part Location") or field(secs, "_head", "Body Part Location"),
        }.items() if v}
        ems = "Emergency Medical Service Required (EMS) , Refused Medical Attention (RMA)"
        r["medical"] = {k: v for k, v in {
            "ems": field(secs, ems, "EMS"), "rma": field(secs, ems, "RMA"),
            "emsArrived": when(field(secs, ems, "Respond Date & Time"), bad_dates),
            "emsName": field(secs, ems, "EMS Name"), "emsNumber": field(secs, ems, "EMS #"),
            "hospital": field(secs, ems, "Hospital Name"),
            "factor": field(secs, "Contributing Factors, Symptoms", "Contributing Factors"),
            "consumed": when(field(secs, "Contributing Factors, Symptoms", "Consumed Date & Time"), bad_dates),
            "symptoms": free(secs, "Contributing Factors, Symptoms"),
        }.items() if v}
    r["subject"] = person(secs, form, "Subject")
    if form == "HI":
        r["victim"] = person(secs, form, "Victim")
    pol = secs.get("Police Involvement", [])
    if any(i[0] for i in pol):
        unk = unknown(secs, "Police Involvement")
        r["police"] = {k: v for k, v in {
            "called": when(field(secs, "Police Involvement", "Date & Time"), bad_dates),
            "agency": unk[0][1] if unk else None,
            "officer": field(secs, "Police Involvement", "Officer Name"),
            "caseNumber": field(secs, "Police Involvement", "Police Case #"),
        }.items() if v}
    wit = []
    for n in ("", " 2"):
        w = {k: v for k, v in {
            "name": field(secs, "Witnesses", f"Witness{n} Name"),
            "phone": field(secs, "Witnesses", f"Witness{n} Phone"),
            "address": field(secs, "Witnesses", f"Witness{n} Address"),
        }.items() if v}
        if w.get("name"):
            wit.append(w)
    r["witnesses"] = wit
    veh = "Does Incident involve a Vehicle ?"
    if field(secs, veh, "Vehicle Involved ?"):
        plate = field(secs, veh, "License Plate/State", 0)
        ticket = next((v for lbl, v in unknown(secs, veh) if "Ticket" in lbl), None)
        r["vehicle"] = {k: v for k, v in {
            "circumstance": field(secs, veh, "Circumstances"), "make": field(secs, veh, "Vehicle Make"),
            "model": field(secs, veh, "Vehicle Model"), "year": field(secs, veh, "Vehicle Year"),
            "color": field(secs, veh, "Vehicle Color"), "plate": plate,
            "plateState": field(secs, veh, "License Plate/State", 1), "vin": field(secs, veh, "VIN #"),
            "valetTicket": ticket, "operatorType": field(secs, veh, "Vehicle Operator"),
            "operatorName": field(secs, veh, "Operator Name"), "operatorDob": field(secs, veh, "Operator DOB"),
            "operatorLicense": field(secs, veh, "Operator D.L"), "operatorPhone": field(secs, veh, "Operator Phone"),
            "ownerName": field(secs, veh, "Vehicle Owner Name"), "ownerAddress": field(secs, veh, "Vehicle Owner Address"),
            "ownerPhone": field(secs, veh, "Vehicle Owner Phone"), "insurance": field(secs, veh, "Insurance Company"),
            "rental": field(secs, veh, "Rental Company"),
            "damage": free(secs, veh, min_len=2),
        }.items() if v}
    pic = "Pictures / Video Taken ?"
    media = {k: v for k, v in {
        "pictures": field(secs, pic, "Pictures Taken ?"), "video": field(secs, pic, "Video Taken ?"),
        "picturesTaken": when(field(secs, pic, "Pictures Date & Time"), bad_dates),
        "videoTaken": when(field(secs, pic, "Video Date & Time"), bad_dates),
        "pictureCount": field(secs, pic, "Number of Pictures"), "videoDescription": field(secs, pic, "Video Description"),
        "takenBy": field(secs, pic, "Pictures Taken By"),
    }.items() if v}
    if media:
        r["media"] = media
    ev = "Was Evidence Collected ?"
    evidence = {k: v for k, v in {
        "items": field(secs, ev, "Item Description"), "date": when(field(secs, ev, "Date & Time"), bad_dates),
        "stored": field(secs, ev, "Location Stored"), "disposition": field(secs, ev, "Disposition"),
    }.items() if v}
    if evidence:
        r["evidence"] = evidence
    # Property Missing/Damaged: a header row of 6 span.style22 cells, then 6 values per item.
    items = []
    in_prop = False
    row = []
    for kind, text in leaves:
        if kind in HEADING_KINDS and text in SECTIONS:
            in_prop = text == "Property Missing/Damaged"
            continue
        if in_prop and kind in VALUE_KINDS:
            row.append(text)
            if len(row) == 6:
                items.append(dict(zip(["item", "description", "color", "brand", "modelSerial", "claimValue"],
                                      [None if v in EMPTY else v for v in row])))
                row = []
    r["propertyItems"] = items
    r["totalClaim"] = field(secs, "Total Claim Value", "Total Claim Value") or free(secs, "Total Claim Value")
    r["hotelProperty"] = free(secs, "Hotel Property Involved ?")
    r["pictureFiles"] = sum(1 for item in secs.get("Pictures", []) if item[1])
    r["pdfFiles"] = sum(1 for item in secs.get("PDF Attachments", []) if item[1])
    return {k: v for k, v in r.items() if v not in (None, "", [], {})}, secs


def main():
    os.makedirs(OUT, exist_ok=True)
    out, filled, unknowns, bad_dates = [], Counter(), Counter(), Counter()
    per_form = Counter()
    for form in FORMS:
        data = json.load(open(f"{SRC}/msshift-incidents-{form}.json", encoding="utf-8"))
        for rec in data["records"]:
            if not rec.get("html"):
                continue
            r, secs = parse(form, rec["html"], bad_dates)
            out.append(r)
            per_form[form] += 1
            for k, v in r.items():
                filled[(form, k)] += 1
                if isinstance(v, dict):
                    for k2 in v:
                        filled[(form, f"{k}.{k2}")] += 1
            for name, items in secs.items():
                for item in items:
                    if item[0] and item[0].startswith("?"):
                        unknowns[(form, name[:30], len(item[0]) - 1)] += 1
    json.dump(out, open(f"{OUT}/incidents.json", "w", encoding="utf-8"), ensure_ascii=False)
    print(f"parsed {len(out)} reports: " + ", ".join(f"{f} {n}" for f, n in per_form.items()))
    for form in FORMS:
        keys = sorted(k for f, k in filled if f == form)
        print(f"\n{form} ({per_form[form]}): " + ", ".join(f"{k} {filled[(form, k)]}" for k in keys))
    print("\nunknown labels (form / section / length): " + ", ".join(f"{f}/{s}/{n}:{c}" for (f, s, n), c in sorted(unknowns.items())))
    print("unparsed dates by length: " + (", ".join(f"{n}ch:{c}" for n, c in bad_dates.items()) or "none"))
    dupes = Counter((r["form"], r.get("number")) for r in out)
    print("duplicate numbers: " + str(sum(1 for c in dupes.values() if c > 1)) + ", missing numbers: " +
          str(sum(1 for r in out if not r.get("number"))) + ", statuses: " + str(dict(Counter(r.get("status") for r in out))))


if __name__ == "__main__":
    sys.exit(main())
