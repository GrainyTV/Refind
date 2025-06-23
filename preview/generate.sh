#!/usr/bin/env bash

# Generate color palette to avoid glitching
ffmpeg -i screenshot_%03d.bmp -vf palettegen palette.png

# Generate gif
ffmpeg -f concat -i frames.txt -i palette.png -filter_complex "fps=10,scale=1280:-1:flags=lanczos[x];[x]paletteuse" -loop 0 preview.gif