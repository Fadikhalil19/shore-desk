"""Structure survey: which tag/class combinations the report pages use and whether
their text looks like fixed labels (few distinct texts, repeated on many pages) or
values (many distinct texts). Prints CLASS NAMES AND COUNTS ONLY, never text,
because the files hold personal and incident data.

    python -I tools/msshift-export/classes.py C:/DeskData/import/msshift/msshift-incidents-IR.json
"""
import json
import sys
from collections import Counter, defaultdict
from html.parser import HTMLParser

KEEP = ("td", "span", "a", "b", "div")


class Kinds(HTMLParser):
    """Text of each element kind (tag.class), innermost element wins."""

    def __init__(self):
        super().__init__()
        self.stack, self.texts = [], defaultdict(list)

    def handle_starttag(self, tag, attrs):
        if tag in KEEP:
            self.stack.append([f"{tag}.{dict(attrs).get('class') or '-'}", tag, []])

    def handle_endtag(self, tag):
        for i in range(len(self.stack) - 1, -1, -1):
            if self.stack[i][1] == tag:
                kind, _, buf = self.stack.pop(i)
                text = " ".join("".join(buf).split())
                if text:
                    self.texts[kind].append(text)
                return

    def handle_data(self, data):
        if self.stack:
            self.stack[-1][2].append(data)


def main(path):
    data = json.load(open(path, encoding="utf-8"))
    pages = []
    for r in data["records"]:
        if r.get("html"):
            p = Kinds()
            p.feed(r["html"])
            pages.append(p.texts)
    n = len(pages)
    print(f"{path}: {n} pages")
    print(f"  {'kind':<28} {'cells':>7} {'distinct':>8} {'in>=10%':>8}")
    kinds = Counter(k for p in pages for k in p for _ in p[k])
    for kind, cells in kinds.most_common(40):
        on = Counter(t for p in pages for t in set(p.get(kind, [])))
        common = sum(1 for t, c in on.items() if c >= 0.1 * n)
        print(f"  {kind:<28} {cells:>7} {len(on):>8} {common:>8}")


if __name__ == "__main__":
    main(sys.argv[1])
