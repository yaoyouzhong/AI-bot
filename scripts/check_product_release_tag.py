#!/usr/bin/env python3
"""Validate a product tag without mistaking it for a bridge component tag."""
import sys
from product_release import valid_product_tag

if len(sys.argv) != 2 or not valid_product_tag(sys.argv[1]):
    raise SystemExit('Invalid AI-bot product release tag')
print('Product release tag verified:', sys.argv[1])
