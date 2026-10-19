"""Runs Philter (philter-ucsf 1.0.3) in its default configuration on a folder of notes.

    python run-philter.py <export folder>/text <output folder>

This is what Philter's own command line does with --prod=True, except that the output is its
asterisk format (each letter and digit it removes replaced by "*"), which
tools/baselines/to-predictions.cs turns into spans. Its --prod mode ignores --outputformat and
writes XML that lists only what its blacklists matched; its evaluation mode tries to download
Stanford NER, which the default configuration doesn't use. Neither changes what it removes.

The notes and the output hold corpus text: keep both folders out of Git. Nothing is printed.
"""

import os
import sys

from philter_ucsf.philter import Philter


def main() -> int:
    try:
        notes, output = (os.path.join(os.path.abspath(p), "") for p in sys.argv[1:])
    except ValueError:
        print("Usage: python run-philter.py <folder of .txt notes> <output folder>", file=sys.stderr)
        return 2

    os.makedirs(output, exist_ok=True)

    # The default configuration names its filters relative to the package's own folder.
    os.chdir(os.path.dirname(os.path.abspath(sys.modules["philter_ucsf"].__file__)))
    philter = Philter(
        {
            "verbose": False,
            "run_eval": False,
            "finpath": notes,
            "foutpath": output,
            "outformat": "asterisk",
            "filters": "./configs/philter_delta.json",
            "cachepos": None,
        }
    )
    philter.map_coordinates()
    philter.transform()
    return 0


if __name__ == "__main__":
    sys.exit(main())
