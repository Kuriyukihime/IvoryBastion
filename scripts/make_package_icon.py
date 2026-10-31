"""Restore the original mobile portrait used by the published 0.32.0 package.

The preserved source is the exact icon.png from that immutable release.
Siege renders remain available for model reviews, without replacing this icon.
"""
from pathlib import Path
import hashlib

ROOT=Path(__file__).resolve().parents[1]

def generate():
    portrait=(ROOT/'assets/source/package_mobile_icon.png').read_bytes()
    if hashlib.sha256(portrait).hexdigest()!='12022af9291e7e38561d349a298006e733e623a834670d2b93d05b469349bf9a':
        raise ValueError('The preserved original Ivory Bastion launcher portrait changed.')
    (ROOT/'package/icon.png').write_bytes(portrait)

if __name__=='__main__':
    generate()
