#!/usr/bin/env python3
"""Check Cobertura branch coverage against a required floor."""

from __future__ import annotations

import argparse
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("report_root", type=Path, help="Directory containing coverage.cobertura.xml files.")
    parser.add_argument("--minimum-branch-rate", type=float, default=80.0, help="Minimum branch coverage percentage. Default: 80.")
    parser.add_argument("--warn-only", action="store_true", help="Emit a GitHub Actions warning instead of failing when coverage is below the floor.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if not 0 <= args.minimum_branch_rate <= 100:
        print("Minimum branch coverage must be between 0 and 100.", file=sys.stderr)
        return 2

    reports = sorted(args.report_root.rglob("coverage.cobertura.xml"), key=lambda path: path.stat().st_mtime_ns, reverse=True)
    if not reports:
        print(f"No coverage.cobertura.xml report was found under {args.report_root}.", file=sys.stderr)
        return 2

    try:
        coverage = ET.parse(reports[0]).getroot()
        branch_rate = float(coverage.attrib["branch-rate"]) * 100
    except (ET.ParseError, KeyError, ValueError) as exception:
        print(f"Coverage report {reports[0]} is invalid: {exception}", file=sys.stderr)
        return 2

    print(f"Branch coverage: {branch_rate:.2f}% (required: {args.minimum_branch_rate:.2f}%).")
    if branch_rate < args.minimum_branch_rate:
        message = f"Branch coverage is {branch_rate:.2f}%, below the {args.minimum_branch_rate:.2f}% floor."
        if args.warn_only:
            print(f"::warning title=Library branch coverage::{message}")
            return 0

        print(message, file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
