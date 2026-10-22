#!/usr/bin/env python3
"""Testes sintéticos de chroma_key.py e slice_strip.py. Uso: python tools/art/test_art_tools.py"""
import sys
import unittest
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, str(Path(__file__).resolve().parent))
import chroma_key  # noqa: E402
import slice_strip  # noqa: E402


def green_disc(size=200, radius=60, color=(200, 60, 40)):
    """Disco vermelho num fundo verde puro, com borda anti-aliased (suavizada por blur)."""
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).ellipse([size // 2 - radius, size // 2 - radius, size // 2 + radius, size // 2 + radius], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(1.2))
    fg = Image.new("RGB", (size, size), color)
    bg = Image.new("RGB", (size, size), (0, 255, 0))
    return Image.composite(fg, bg, mask), mask


class ChromaKeyTests(unittest.TestCase):
    def test_background_becomes_transparent_and_disc_stays_opaque(self):
        src, _ = green_disc()
        out = np.asarray(chroma_key.chroma_key(src))
        self.assertEqual(out[2, 2, 3], 0)
        self.assertEqual(out[100, 100, 3], 255)

    def test_edge_has_no_green_fringe(self):
        src, _ = green_disc()
        out = np.asarray(chroma_key.chroma_key(src)).astype(int)
        edge = (out[..., 3] > 0) & (out[..., 3] < 255)
        self.assertGreater(edge.sum(), 50, "deveria haver pixels de borda anti-aliased")
        excess = out[..., 1][edge] - np.maximum(out[..., 0][edge], out[..., 2][edge])
        self.assertLessEqual(int(excess.max()), 6, "franja verde remanescente na borda")

    def test_edge_alpha_is_soft(self):
        src, mask = green_disc()
        out = np.asarray(chroma_key.chroma_key(src)).astype(int)
        err = np.abs(out[..., 3] - np.asarray(mask).astype(int))
        self.assertLess(err.mean(), 3.0)

    def test_genuine_green_content_survives(self):
        src, _ = green_disc(color=(70, 150, 60))  # folha verde-musgo
        out = np.asarray(chroma_key.chroma_key(src))
        self.assertEqual(out[100, 100, 3], 255)


class SliceStripTests(unittest.TestCase):
    def make_strip(self, frames=4, slot=120):
        strip = Image.new("RGBA", (slot * frames, 150), (0, 0, 0, 0))
        draw = ImageDraw.Draw(strip)
        for i in range(frames):
            wobble = (i % 2) * 14  # corpo balança para os lados e o pé sobe/desce
            cx = i * slot + 40 + wobble
            foot = 130 - (i % 3) * 6
            draw.rectangle([cx - 10, foot - 90, cx + 10, foot], fill=(180, 90, 60, 255))
        return strip

    def test_feet_land_on_anchor(self):
        sheet = slice_strip.slice_strip(self.make_strip(), 4)
        self.assertEqual(sheet.size, (slice_strip.CELL * 4, slice_strip.CELL))
        for i in range(4):
            cell = np.asarray(sheet.crop((i * slice_strip.CELL, 0, (i + 1) * slice_strip.CELL, slice_strip.CELL)).getchannel("A"))
            rows = np.where((cell >= 24).any(axis=1))[0]
            self.assertEqual(int(rows[-1]), slice_strip.FOOT_Y)
            cols = np.where((cell[rows[-1] - 5 : rows[-1] + 1] >= 24).any(axis=0))[0]
            self.assertAlmostEqual(float(cols.mean()), slice_strip.FOOT_X, delta=1.0)

    def test_oversized_frame_is_rejected(self):
        strip = Image.new("RGBA", (400, 300), (0, 0, 0, 0))
        ImageDraw.Draw(strip).rectangle([10, 0, 190, 299], fill=(255, 255, 255, 255))
        with self.assertRaises(ValueError):
            slice_strip.slice_strip(strip, 2)


if __name__ == "__main__":
    unittest.main(verbosity=2)
