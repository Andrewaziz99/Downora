"""Merges the per-project Cobertura reports in TestResults/ and fails when line coverage drops too low.

Usage (after `dotnet test ... --coverage --coverage-output-format cobertura --coverage-output cov-<Project>.xml
--results-directory TestResults` for each test project):

    python tools/coverage.py

The WPF views (Downora.App) have no unit tests and are not part of the gate; the logic behind them (view models,
services) is. Thresholds sit a little below today's numbers so only a real drop fails the build.
"""

import collections
import glob
import sys
import xml.etree.ElementTree as ET

# Assembly -> minimum line coverage (percent).
THRESHOLDS = {
    "Downora.Core": 92.0,
    "Downora.Infrastructure": 88.0,
    "Downora.Presentation": 90.0,
}
TOTAL_THRESHOLD = 90.0


def main() -> int:
    reports = glob.glob("TestResults/cov-*.xml")
    if not reports:
        print("No coverage reports found in TestResults/ (run the tests with --coverage first).")
        return 2

    # A line counts as covered when any test project executed it.
    hits: dict[tuple[str, int], int] = collections.defaultdict(int)
    lines_of: dict[tuple[str, str], set[tuple[str, int]]] = collections.defaultdict(set)
    for report in reports:
        root = ET.parse(report).getroot()
        for package in root.iter("package"):
            for cls in package.iter("class"):
                file_name = cls.get("filename")
                for line in cls.iter("line"):
                    key = (file_name, int(line.get("number")))
                    hits[key] = max(hits[key], int(line.get("hits")))
                    lines_of[(package.get("name"), file_name)].add(key)

    per_assembly: dict[str, list[int]] = collections.defaultdict(lambda: [0, 0])
    for (assembly, _), keys in lines_of.items():
        per_assembly[assembly][0] += sum(1 for k in keys if hits[k] > 0)
        per_assembly[assembly][1] += len(keys)

    failed = False
    total_hit = total_lines = 0
    print(f"{'Assembly':<28}{'Lines':>10}{'Coverage':>10}{'Minimum':>10}")
    for assembly, (hit, lines) in sorted(per_assembly.items()):
        if not assembly.startswith("Downora.") or ".Tests" in assembly:
            continue
        percent = 100.0 * hit / lines
        minimum = THRESHOLDS.get(assembly)
        status = ""
        if minimum is not None and percent < minimum:
            status = "  <-- below the minimum"
            failed = True
        print(f"{assembly:<28}{lines:>10}{percent:>9.1f}%{(minimum or 0):>9.1f}%{status}")
        total_hit += hit
        total_lines += lines

    total = 100.0 * total_hit / total_lines
    print(f"{'Total':<28}{total_lines:>10}{total:>9.1f}%{TOTAL_THRESHOLD:>9.1f}%")
    if total < TOTAL_THRESHOLD:
        failed = True

    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
