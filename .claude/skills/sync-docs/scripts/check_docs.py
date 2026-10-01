"""Consistency checks for the Daisy docs.

Usage (from the repository root):
    python .claude/skills/sync-docs/scripts/check_docs.py [--stale TERM ...]

Checks:
  1. Every BR / UC / sequence file is listed in its README, and every README entry exists.
  2. Every relative markdown link in .claude/ and docs/md/ points to an existing file.
  3. Every use case has a sequence diagram and vice versa.
  4. BR "Used by" and UC "Business rules" reference each other (both directions).
  5. Sequence diagrams have @startuml/@enduml and balanced alt/opt/loop/group ... end blocks.
  6. Every endpoint in the sequences README "Endpoint Summary" is documented in API_DESIGN.md.
  7. DaisyDB.sql header table count matches the number of CREATE TABLE statements.
  8. Stale terms (default: "Cancelled") do not appear in the docs.

Exit code 1 if any ERROR is found; WARN lines do not fail the run.
"""

import argparse
import io
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
CLAUDE = os.path.join(ROOT, ".claude")
BR_DIR = os.path.join(CLAUDE, "business-rules")
UC_DIR = os.path.join(CLAUDE, "use-cases")
SEQ_DIR = os.path.join(CLAUDE, "sequences")
DOCS_MD = os.path.join(ROOT, "docs", "md")
SQL = os.path.join(ROOT, "infra", "database", "DaisyDB.sql")

errors, warnings = [], []


def err(msg):
    errors.append(msg)


def warn(msg):
    warnings.append(msg)


def read(path):
    with io.open(path, encoding="utf-8") as f:
        return f.read()


def rel(path):
    return os.path.relpath(path, ROOT).replace("\\", "/")


def ids_in_dir(folder, prefix, ext):
    pat = re.compile(r"^(%s-\d+)\.%s$" % (prefix, ext))
    return {m.group(1) for f in os.listdir(folder) for m in [pat.match(f)] if m}


def table_row(text, field):
    m = re.search(r"^\|\s*%s\s*\|(.*)\|\s*$" % re.escape(field), text, re.M)
    return m.group(1) if m else ""


# 1. README indexes ------------------------------------------------------------
def check_indexes():
    for folder, prefix, ext in ((BR_DIR, "BR", "md"), (UC_DIR, "UC", "md"), (SEQ_DIR, "UC", "puml")):
        readme = os.path.join(folder, "README.md")
        if not os.path.exists(readme):
            err("%s is missing" % rel(readme))
            continue
        listed = set(re.findall(r"\]\((%s-\d+)\.%s\)" % (prefix, ext), read(readme)))
        files = ids_in_dir(folder, prefix, ext)
        for i in sorted(files - listed):
            err("%s/%s.%s is not listed in %s" % (rel(folder), i, ext, rel(readme)))
        for i in sorted(listed - files):
            err("%s lists %s.%s, which does not exist" % (rel(readme), i, ext))


# 2. Relative links ----------------------------------------------------------------
def check_links():
    for base in (CLAUDE, DOCS_MD):
        for dirpath, _, filenames in os.walk(base):
            if os.path.basename(dirpath) == "logs":
                continue
            for name in filenames:
                if not name.endswith(".md"):
                    continue
                path = os.path.join(dirpath, name)
                text = re.sub(r"```.*?```", "", read(path), flags=re.S)  # ignore templates in code blocks
                for target in re.findall(r"\]\(([^)\s]+)\)", text):
                    if re.match(r"^(https?:|mailto:|#)", target):
                        continue
                    target_path = os.path.normpath(os.path.join(dirpath, target.split("#")[0]))
                    if not os.path.exists(target_path):
                        err("%s links to missing %s" % (rel(path), target))


# 3 + 4. Use cases, diagrams and business rule cross-references ----------------------
def check_cross_refs():
    ucs = ids_in_dir(UC_DIR, "UC", "md")
    seqs = ids_in_dir(SEQ_DIR, "UC", "puml")
    for i in sorted(ucs - seqs):
        err("%s has no sequence diagram %s.puml" % (i, i))
    for i in sorted(seqs - ucs):
        err("Sequence %s.puml has no use case %s.md" % (i, i))

    uc_to_br = {
        uc: set(re.findall(r"BR-\d+", table_row(read(os.path.join(UC_DIR, uc + ".md")), "Business rules")))
        for uc in ucs
    }
    br_to_uc = {
        br: set(re.findall(r"UC-\d+", table_row(read(os.path.join(BR_DIR, br + ".md")), "Used by")))
        for br in ids_in_dir(BR_DIR, "BR", "md")
    }
    for br, used_by in sorted(br_to_uc.items()):
        for uc in sorted(used_by):
            if uc not in uc_to_br:
                err("%s 'Used by' lists %s, which does not exist" % (br, uc))
            elif br not in uc_to_br[uc]:
                warn("%s 'Used by' lists %s, but %s 'Business rules' does not list %s" % (br, uc, uc, br))
    for uc, brs in sorted(uc_to_br.items()):
        for br in sorted(brs):
            if br not in br_to_uc:
                err("%s 'Business rules' lists %s, which does not exist" % (uc, br))
            elif uc not in br_to_uc[br]:
                warn("%s 'Business rules' lists %s, but %s 'Used by' does not list %s" % (uc, br, br, uc))


# 5. PlantUML structure ----------------------------------------------------------------
def check_puml():
    opener = re.compile(r"^(alt|opt|loop|group|par|critical|break)\b")
    for name in sorted(os.listdir(SEQ_DIR)):
        if not name.endswith(".puml"):
            continue
        text = read(os.path.join(SEQ_DIR, name))
        if "@startuml" not in text or "@enduml" not in text:
            err("%s: missing @startuml or @enduml" % name)
        depth = 0
        for n, line in enumerate(text.splitlines(), 1):
            t = line.strip()
            if opener.match(t):
                depth += 1
            elif t == "end":
                depth -= 1
            elif t.startswith("else") and depth < 1:
                err("%s:%d: 'else' outside a block" % (name, n))
            if depth < 0:
                err("%s:%d: 'end' without an open block" % (name, n))
                depth = 0
        if depth:
            err("%s: %d block(s) not closed with 'end'" % (name, depth))


# 6. Endpoints ------------------------------------------------------------------------------
def norm_endpoint(method, path):
    return method.upper() + " " + re.sub(r"\{[^}]+\}", "{}", path)


def check_endpoints():
    readme = read(os.path.join(SEQ_DIR, "README.md"))
    api_path = os.path.join(DOCS_MD, "API_DESIGN.md")
    if not os.path.exists(api_path):
        err("docs/md/API_DESIGN.md is missing")
        return
    api = read(api_path)
    documented = {norm_endpoint(m, p) for m, p in re.findall(r"\|\s*(GET|POST|PUT|PATCH|DELETE)\s*\|\s*`([^`]+)`", api)}
    summary = readme.split("## Endpoint Summary", 1)[-1]
    for m, p in re.findall(r"\|\s*(GET|POST|PUT|PATCH|DELETE)\s*\|\s*`([^`]+)`", summary):
        if norm_endpoint(m, p) not in documented:
            err("Endpoint %s %s (sequences README) is not in API_DESIGN.md" % (m, p))


# 7. Database header -------------------------------------------------------------------------
def check_sql():
    if not os.path.exists(SQL):
        return
    text = read(SQL)
    count = len(re.findall(r"^CREATE TABLE", text, re.M))
    for label, m in (("header", re.search(r"Database version: (\d+) tables", text)),
                     ("PRINT message", re.search(r"\((\d+) tables", text))):
        if m and int(m.group(1)) != count:
            err("DaisyDB.sql %s says %s tables, but the script creates %d" % (label, m.group(1), count))


# 8. Stale terms --------------------------------------------------------------------------------
def check_stale(terms):
    paths = [SQL]
    for base in (CLAUDE, DOCS_MD):
        for dirpath, _, filenames in os.walk(base):
            # frontend-design is not project docs; sync-docs mentions stale terms on purpose
            if "frontend-design" in dirpath or "sync-docs" in dirpath or os.path.basename(dirpath) == "logs":
                continue
            paths += [os.path.join(dirpath, f) for f in filenames if f.endswith((".md", ".puml"))]
    for path in paths:
        if not os.path.exists(path):
            continue
        for n, line in enumerate(read(path).splitlines(), 1):
            for term in terms:
                if re.search(r"\b%s\b" % re.escape(term), line):
                    err("%s:%d: stale term '%s'" % (rel(path), n, term))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--stale", nargs="*", default=["Cancelled"],
                        help="terms that must no longer appear (default: Cancelled)")
    args = parser.parse_args()

    check_indexes()
    check_links()
    check_cross_refs()
    check_puml()
    check_endpoints()
    check_sql()
    check_stale(args.stale)

    for w in warnings:
        print("WARN  " + w)
    for e in errors:
        print("ERROR " + e)
    print("\n%d error(s), %d warning(s)" % (len(errors), len(warnings)))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
