#!/usr/bin/env python3
"""Static gate for the com.natteens.fynite package.

Everything here runs without Unity, without a licence and without network access, so it can gate a
pull request on any runner. It is deliberately not a substitute for the Unity test suites: it proves
the package is shaped correctly, never that it behaves correctly. The release workflow requires both.

Run from the repository root:

    python3 .github/scripts/validate_package.py            # the checks a PR must pass
    python3 .github/scripts/validate_package.py --release  # everything, including release-only rules

Exit code is 0 when every check passed and 1 otherwise. Each failure names the file and the reason.
"""

import argparse
import json
import os
import re
import subprocess
import sys

PACKAGE_NAME = "com.natteens.fynite"
SEMVER = re.compile(r"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$")
TAG = re.compile(r"^v(\d+)\.(\d+)\.(\d+)$")

# Reflection over non-public members, per file, with the reason it is tolerated. Anything not listed
# here is a failure: the point is that a new one has to be argued for, not merely written.
NONPUBLIC_REFLECTION_ALLOWED = {
    "Editor/Graph/Nodes/FyniteBlockNodeBase.cs":
        "enumerates [SerializeField] fields of user block types, the same rule Unity serializes by; "
        "the types reflected over are the user's own, never Graph Toolkit's",
    "Tests/Editor/RunnerEditModeTests.cs":
        "invokes Fynite's own private OnValidate to test authoring-time context resolution",
    "Tests/EditorGraph/GraphAuthoringTests.cs":
        "reads Fynite's own private m_FyniteGuid to assert identity survives a round trip",
    "Tests/EditorGraph/RecordingGraphLogger.cs":
        "GraphLogger exposes no public sink; documented Graph Toolkit limitation, test scaffolding only",
}

# Paths Unity never imports, so they need no .meta and may hold anything.
META_EXEMPT_PREFIXES = (".github/",)


class Report:
    def __init__(self):
        self.failures = []
        self.notes = []

    def fail(self, check, detail):
        self.failures.append((check, detail))

    def note(self, text):
        self.notes.append(text)


def run(*args):
    return subprocess.run(args, capture_output=True, text=True, check=False).stdout


def tracked_files():
    return [line for line in run("git", "ls-files", "-z").split("\0") if line]


def read(path):
    try:
        with open(path, encoding="utf-8", errors="replace") as handle:
            return handle.read()
    except OSError:
        # Tracked but not on disk. Reported once by the meta check rather than as a crash here.
        return ""


def tags():
    found = []
    for line in run("git", "tag", "-l").splitlines():
        match = TAG.match(line.strip())
        if match:
            found.append((tuple(int(p) for p in match.groups()), line.strip()))
    found.sort()
    return found


def source_files(files, *roots):
    return [f for f in files if f.endswith(".cs") and f.startswith(roots)]


# ── checks ──────────────────────────────────────────────────────────────────────────────────────

def check_package_json(report, files):
    if "package.json" not in files:
        report.fail("package.json", "not tracked")
        return None

    try:
        manifest = json.loads(read("package.json"))
    except json.JSONDecodeError as error:
        report.fail("package.json", "is not valid JSON: %s" % error)
        return None

    for field in ("name", "version", "displayName", "description", "unity"):
        if not manifest.get(field):
            report.fail("package.json", "missing required field '%s'" % field)

    if manifest.get("name") != PACKAGE_NAME:
        report.fail("package.json", "name is '%s', expected '%s'" % (manifest.get("name"), PACKAGE_NAME))

    version = manifest.get("version", "")
    if not SEMVER.match(version):
        report.fail("package.json", "version '%s' is not a plain semver release" % version)

    for sample in manifest.get("samples", []):
        path = sample.get("path", "")
        if not path or not os.path.isdir(path):
            report.fail("package.json", "sample path '%s' does not exist" % path)

    return manifest


def check_version_matches_tag(report, manifest, release_mode):
    if manifest is None:
        return

    version = manifest.get("version", "")
    published = tags()

    if not published:
        report.note("no release tags yet; skipped the version/tag agreement check")
        return

    latest = published[-1][1]

    if latest != "v" + version:
        detail = ("package.json says %s but the newest tag is %s. On main these must agree: "
                  "semantic-release writes the version it just published." % (version, latest))
        if release_mode:
            report.fail("version", detail)
        else:
            report.note("version: " + detail)


def check_runtime_purity(report, files):
    banned = {
        "UnityEditor": re.compile(r"\bUnityEditor\b"),
        "Unity.GraphToolkit": re.compile(r"\bUnity\.GraphToolkit\b|\bGraphToolkit\b"),
    }

    for path in source_files(files, "Runtime/", "Samples~/BasicHfsm/Runtime/"):
        text = read(path)
        for label, pattern in banned.items():
            if pattern.search(text):
                report.fail("runtime-purity", "%s references %s; runtime code ships in players" % (path, label))


def check_removed_symbols(report, files):
    # These are the rejected parallel-editor types. They must not come back under any name, and no
    # production file may mention them. A test asserting one is absent is the one legitimate mention.
    for symbol, scopes in (
        ("FyniteStateScopeWindow", ("Runtime/", "Editor/", "Tests/", "Samples~/")),
        ("FyniteStateScopeSession", ("Runtime/", "Editor/", "Tests/", "Samples~/")),
        ("FyniteConfigNode", ("Runtime/", "Editor/", "Samples~/")),
        ("FyniteSchemaMigrator", ("Runtime/", "Editor/", "Tests/", "Samples~/")),
        ("FyniteGraphMigration", ("Runtime/", "Editor/", "Tests/", "Samples~/")),
    ):
        pattern = re.compile(r"\b%s\b" % symbol)
        for path in source_files(files, *scopes):
            if pattern.search(read(path)):
                report.fail("removed-symbols", "%s mentions %s, which was removed" % (path, symbol))

        for path in files:
            if os.path.basename(path).startswith(symbol + "."):
                report.fail("removed-symbols", "%s is a file for the removed type %s" % (path, symbol))


def check_reflection(report, files):
    internal_namespace = re.compile(r"Unity\.GraphToolkit\.[A-Za-z.]*Implementation")
    nonpublic = re.compile(r"BindingFlags\.NonPublic")

    for path in source_files(files, "Runtime/", "Editor/", "Tests/", "Samples~/"):
        text = read(path)

        if internal_namespace.search(text):
            report.fail("reflection", "%s names a Graph Toolkit implementation namespace" % path)

        if not nonpublic.search(text):
            continue

        if path in NONPUBLIC_REFLECTION_ALLOWED:
            continue

        if path.startswith(("Runtime/", "Editor/")):
            report.fail("reflection", "%s reflects over non-public members; production code must not" % path)
        else:
            report.fail(
                "reflection",
                "%s reflects over non-public members and is not in the allowlist. Add it to "
                "NONPUBLIC_REFLECTION_ALLOWED with the reason, or use a public API." % path)


def read_int_constant(path, name):
    match = re.search(r"\b%s\s*=\s*(\d+)\s*;" % re.escape(name), read(path))
    return int(match.group(1)) if match else None


def check_schema_and_importer(report, files):
    document = "Editor/Compiler/Authoring/FyniteGraphDocument.cs"
    importer = "Editor/Graph/Import/FyniteGraphImporter.cs"

    if document not in files or importer not in files:
        report.fail("schema", "the schema or importer source is missing")
        return None, None

    schema = read_int_constant(document, "CurrentSchemaVersion")
    importer_version = read_int_constant(importer, "ImporterVersion")

    if schema is None or schema < 1:
        report.fail("schema", "CurrentSchemaVersion is missing or not a positive integer")
    if importer_version is None or importer_version < 1:
        report.fail("schema", "ImporterVersion is missing or not a positive integer")

    # The importer must gate on the shared constant. A literal in that comparison is how a schema
    # bump silently stops being enforced at import time while the error message still looks right.
    gate = re.compile(r"SchemaVersion\s*!=\s*FyniteGraphDocument\.CurrentSchemaVersion")
    if not gate.search(read(importer)):
        report.fail("schema",
                    "%s does not gate on 'SchemaVersion != FyniteGraphDocument.CurrentSchemaVersion'; "
                    "a literal there stops enforcing the schema on the next bump" % importer)

    # Compared against the newest tag, so what it enforces is "between two releases, a schema change
    # is accompanied by an importer bump". A second schema bump inside one release cycle, after the
    # importer already moved, is not caught — by then every project already reimports on the version
    # that did move, so the stale-asset failure this guards against cannot happen.
    published = tags()
    if published and schema is not None and importer_version is not None:
        latest = published[-1][1]
        previous_document = run("git", "show", "%s:%s" % (latest, document))
        previous_importer = run("git", "show", "%s:%s" % (latest, importer))

        if previous_document and previous_importer:
            was_schema = re.search(r"\bCurrentSchemaVersion\s*=\s*(\d+)\s*;", previous_document)
            was_importer = re.search(r"\bImporterVersion\s*=\s*(\d+)\s*;", previous_importer)

            if was_schema and was_importer:
                if int(was_schema.group(1)) != schema and int(was_importer.group(1)) == importer_version:
                    report.fail(
                        "schema",
                        "the schema went from %s to %s since %s but ImporterVersion is still %s. Every "
                        "graph in every project keeps its stale compiled asset until that number moves."
                        % (was_schema.group(1), schema, latest, importer_version))

    return schema, importer_version


def check_fyn_assets(report, files, schema):
    if schema is None:
        return

    graphs = [f for f in files if f.endswith(".fyn")]

    if not graphs:
        report.note("no tracked .fyn assets to check")
        return

    for path in graphs:
        found = re.search(r"m_SchemaVersion:\s*(\d+)", read(path))
        if not found:
            report.fail("fyn-assets", "%s declares no schema version" % path)
        elif int(found.group(1)) != schema:
            report.fail("fyn-assets", "%s is schema %s; this build only accepts %s"
                        % (path, found.group(1), schema))


def check_meta_files(report, files):
    tracked = set(files)
    directories = set()

    for path in files:
        if path.startswith(META_EXEMPT_PREFIXES):
            continue

        parts = path.split("/")
        for depth in range(1, len(parts)):
            directories.add("/".join(parts[:depth]))

        if path.endswith(".meta") or os.path.basename(path).startswith("."):
            continue

        if path + ".meta" not in tracked:
            report.fail("meta", "%s has no tracked .meta; the asset arrives with a fresh GUID in every project"
                        % path)

    for directory in sorted(directories):
        # A folder ending in ~ is not imported by Unity, so it needs no meta of its own. Neither does
        # a sample's own root: the Package Manager creates the destination folder when it copies the
        # sample into Assets. Everything below that does need one, because those folders are copied
        # verbatim and their GUIDs are what keep asmdef and script references intact.
        parent = directory.rsplit("/", 1)[0] if "/" in directory else ""
        if directory.endswith("~") or parent.endswith("~") or directory.startswith(META_EXEMPT_PREFIXES):
            continue
        if directory + ".meta" not in tracked:
            report.fail("meta", "folder %s has no tracked .meta" % directory)

    for path in files:
        if not path.endswith(".meta"):
            continue
        # git tracks files, not folders, so a folder's meta is matched against the folder set.
        target = path[: -len(".meta")]
        if target not in tracked and target not in directories:
            report.fail("meta", "%s is orphaned; the asset it describes is not tracked" % path)

    guids = {}
    for path in files:
        if not path.endswith(".meta"):
            continue
        found = re.search(r"^guid:\s*([0-9a-f]{32})\s*$", read(path), re.MULTILINE)
        if not found:
            report.fail("meta", "%s has no valid 32-character guid" % path)
            continue
        guid = found.group(1)
        if guid in guids:
            report.fail("meta", "%s and %s share the guid %s" % (guids[guid], path, guid))
        guids[guid] = path


def check_changelog(report, files, manifest):
    if "CHANGELOG.md" not in files:
        report.fail("changelog", "CHANGELOG.md is not tracked")
        return

    text = read("CHANGELOG.md")

    for _, tag in tags():
        version = tag[1:]
        if not re.search(r"^#+.*\b%s\b" % re.escape(version), text, re.MULTILINE):
            report.fail("changelog", "%s is a published tag with no heading in CHANGELOG.md" % tag)

    if manifest and manifest.get("version"):
        version = manifest["version"]
        if tags() and not re.search(r"^#+.*\b%s\b" % re.escape(version), text, re.MULTILINE):
            report.fail("changelog", "package.json is %s but the changelog has no heading for it" % version)


def check_clean_tree(report):
    dirty = run("git", "status", "--porcelain").strip()
    if dirty:
        report.fail("clean-tree",
                    "the working tree is not clean, so the tag would not describe what was validated:\n"
                    + "\n".join("    " + line for line in dirty.splitlines()))


WHITESPACE_CHECKED = (".cs", ".py", ".sh", ".yml", ".yaml", ".json", ".md")


def check_whitespace(report, files):
    """Trailing whitespace in files a human writes.

    Not `git diff --check`: that only sees uncommitted changes, so on a CI checkout it has nothing to
    look at, and pointed at the whole history it would drown in Unity's own .meta files, which end
    `userData: ` with a trailing space by design. Checking the source extensions catches the thing
    worth catching and stays quiet about the generated files nobody edits.
    """
    for path in files:
        if not path.endswith(WHITESPACE_CHECKED):
            continue

        for number, line in enumerate(read(path).splitlines(), start=1):
            if line != line.rstrip():
                report.fail("whitespace", "%s:%d has trailing whitespace" % (path, number))
                break


# ── entry point ─────────────────────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="Static gate for the Fynite package.")
    parser.add_argument("--release", action="store_true",
                        help="also apply the rules that only make sense for a revision about to be tagged")
    args = parser.parse_args()

    if not os.path.isfile("package.json"):
        print("error: run this from the package repository root", file=sys.stderr)
        return 1

    report = Report()
    files = tracked_files()

    manifest = check_package_json(report, files)
    check_version_matches_tag(report, manifest, args.release)
    check_runtime_purity(report, files)
    check_removed_symbols(report, files)
    check_reflection(report, files)
    schema, importer_version = check_schema_and_importer(report, files)
    check_fyn_assets(report, files, schema)
    check_meta_files(report, files)
    check_changelog(report, files, manifest)
    check_whitespace(report, files)

    if args.release:
        check_clean_tree(report)

    print("Fynite static validation%s" % (" (release mode)" if args.release else ""))
    print("  tracked files      : %d" % len(files))
    print("  package version    : %s" % (manifest.get("version") if manifest else "?"))
    print("  schema version     : %s" % schema)
    print("  importer version   : %s" % importer_version)
    print("  tracked .fyn assets: %d" % len([f for f in files if f.endswith(".fyn")]))
    print()

    for note in report.notes:
        print("note: %s" % note)

    if report.notes:
        print()

    if not report.failures:
        print("PASS - every static check succeeded.")
        return 0

    print("FAIL - %d problem(s):" % len(report.failures))
    for check, detail in report.failures:
        print("  [%s] %s" % (check, detail))
    return 1


if __name__ == "__main__":
    sys.exit(main())
