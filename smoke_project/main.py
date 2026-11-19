import json
from pathlib import Path

import pygame
import cv2
import moderngl
import mutagen
from helper import VALUE

result = {
    "pygame": pygame.version.ver,
    "opencv": cv2.__version__,
    "moderngl": moderngl.__version__,
    "mutagen": mutagen.version_string,
    "module": VALUE,
    "asset": Path("asset.txt").read_text(encoding="utf-8").strip(),
}
Path("result.json").write_text(json.dumps(result), encoding="utf-8")
