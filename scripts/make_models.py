"""Regenerate the selected Seraph models and their mesh previews."""
from seraph_models import mobile, siege, units
from make_missiles import missiles

if __name__ == "__main__":
    mobile()
    siege()
    units()
    missiles()
