"""Compatibility entry point; previews now use the actual compiled choreography."""
from pathlib import Path
import runpy
runpy.run_path(str(Path(__file__).with_name('review_presentation_poses.py')),run_name='__main__')
