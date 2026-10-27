# Calendar flowers

These twelve transparent PNGs were generated for this project with the built-in
image_gen tool on 2026-10-06, without third-party image references. They replace
the calendar cover's geometric motifs. The display is a Gregorian calendar;
the flower ordering is an artistic choice, not a botanical flowering forecast.

The common prompt requested restrained Chinese gongbi ink contours and mineral
pigment washes, one isolated botanical spray, the entire stem and leaves, no
text, signature, seal, watermark, pot, frame or background. Subject details and
provenance are retained in provenance.json. All original alpha PNGs are kept.

Run `python scripts/generate-calendar-flowers.py` to create the firmware's
calendar_flowers.h. The import step resamples to 280×252, quantizes RGBA to an
indexed palette and uses the existing LVGL LZ4 decoder. encoding.json records
original SHA-256 hashes and encoded sizes; transparent alpha remains present.

The current native composition keeps the original flower in the bottom drawing
layer, at 90% opacity to retain its pigment colors. Four poem columns read from
right to left above it; the month title and solar-term seal sit at the upper right.
Flowers are placed below the verse with a small clear gap so calligraphic strokes
do not overlap petal contours. The title and author form a small lower colophon.
The user-requested roughly 5% reduction is retained (scales 289/281), with
individual offsets based on each botanical silhouette. Original PNGs, 280×252
import resolution, all 256 palette slots, and alpha values remain unchanged.

Months: plum, apricot, peach, tree peony, herbaceous peony, gardenia, lotus,
lily, osmanthus, chrysanthemum, camellia, Chinese narcissus.
