"""Prints the cell skeleton of ONE MSShift report page: every td/span with text, in page
order, as tag.class and either the text (only if it is in the fixed LABELS allow-list
below) or <N chars>. Never prints values: staff names share the label class, so
frequency filters are not safe - only the hard-coded allow-list is.

    python -I tools/msshift-export/skeleton.py <file.json> [record index | "richest" | "with:Label A|Label B"]
"""
import json
import sys
from html.parser import HTMLParser

LABELS = {
    "Report Number", "Incident Date & Time", "Incident Type", "Reported Date & Time", "Incident Location",
    "Written Date & Time", "Injury Type", "Body Part", "Body Part Location", "Subject", "Employee Name",
    "Date of Birth", "Department", "Age", "Position", "Employee Number", "Date of Hire", "Subject Address",
    "Home Street", "Work Street", "Home City", "Work City", "Home State", "Work State", "Home Zip", "Work Zip",
    "Home Country", "Work Country", "Home Phone", "Work Phone", "Incident Narrative - Statements of Facts",
    "Investigation/Follow Up", "EMS", "RMA", "Police Involvement", "Contributing Factors, Symptoms",
    "Contributing Factors", "Witnesses", "Does Incident involve a Vehicle ?", "Pictures / Video Taken ?",
    "Pictures Taken ?", "Video Taken ?", "Pictures Date & Time", "Video Date & Time", "Number of Pictures",
    "Video Description", "Pictures Taken By", "Was Evidence Collected ?", "Report Distributed To",
    "Report Incident to Claims", "Reporting Party", "Reported By", "Written By", "Reviewed By", "Safety",
    "Subject Name", "Guest Room Number", "Room Number", "Subject Type", "Arrival - Departure", "Gender",
    "Group/Company", "Email", "Victim", "Victim Name", "Victim Type", "Victim Address", "Hotel Property Involved ?",
    "Total Claim Value", "Claim Value", "Property Missing/Damaged", "Item", "Description", "Primary Color", "Brand",
    "Model / Serial", "Lock Interrogation", "Witness Name", "Witness 2 Name", "Witness Phone", "Witness 2 Phone",
    "Witness Address", "Witness 2 Address", "Respond Date & Time", "EMS Name", "Officer Name", "Item Description",
    "Date & Time", "Location Stored", "Disposition", "Vehicle Involved ?", "Circumstances", "Vehicle Operator",
    "Operator Name", "Operator DOB", "Operator D.L", "Operator Phone", "Vehicle Owner Name", "Vehicle Owner Address",
    "Vehicle Owner Phone", "Height, Weight", "Eyes , Hair", "Type Detail", "D.L #, D.L State", "Race",
    "Scars/Marks/Tattoos", "Date of Birth, Age", "Yes", "No", "N/A", "X", "Handover to Shift *",
    "Handover to Date *", "Add To Handover:", "Notice of Report on File", "PDF Attachments", "Pictures",
    "Agency", "Badge", "Shield", "Case Number", "Police Agency", "Officer Badge", "Report Number:",
    "Emergency Medical Service Required (EMS) , Refused Medical Attention (RMA)", "Guest", "Employee",
    "Non Guest", "Hotel Property", "Pedestrian", "Incident Report", "Guest Accident/Illness Report",
    "Employee Accident/Illness Report", "Property Missing/Damaged Report", "High Level Incident Report",
    "View modifications history", "Generating PDF Please Wait...",
}


class Skeleton(HTMLParser):
    def __init__(self):
        super().__init__()
        self.stack, self.out = [], []

    def handle_starttag(self, tag, attrs):
        if tag in ("td", "span", "a", "img", "input", "textarea"):
            a = dict(attrs)
            kind = f"{tag}.{a.get('class') or '-'}"
            if tag in ("img", "input"):
                self.out.append((len(self.stack), kind + f"[{a.get('type') or ''}]", ""))
            else:
                self.stack.append([tag, kind, []])

    def handle_endtag(self, tag):
        for i in range(len(self.stack) - 1, -1, -1):
            if self.stack[i][0] == tag:
                _, kind, buf = self.stack.pop(i)
                text = " ".join("".join(buf).split())
                if text:
                    self.out.append((i, kind, text if text in LABELS else f"<{len(text)} chars>"))
                return

    def handle_data(self, data):
        if self.stack:
            self.stack[-1][2].append(data)


def main(path, which="richest"):
    data = json.load(open(path, encoding="utf-8"))
    pages = [r["html"] for r in data["records"] if r.get("html")]
    if which == "richest":
        html = max(pages, key=lambda h: sum(lbl in h for lbl in LABELS))
    elif which.startswith("with:"):
        need = which[5:].split("|")
        html = max(pages, key=lambda h: (sum(n in h for n in need), sum(lbl in h for lbl in LABELS)))
    else:
        html = pages[int(which)]
    p = Skeleton()
    p.feed(html)
    for depth, kind, text in p.out:
        print(f"{'  ' * min(depth, 6)}{kind:<22} {text}")


if __name__ == "__main__":
    main(*sys.argv[1:])
