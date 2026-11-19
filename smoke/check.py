import json
import pathlib
import sys
from helper import VALUE

payload = {"value": VALUE, "argument": sys.argv[1], "interpreter": sys.executable}
pathlib.Path(__file__).with_name("result.json").write_text(json.dumps(payload), encoding="utf-8")
print("smoke ok")
