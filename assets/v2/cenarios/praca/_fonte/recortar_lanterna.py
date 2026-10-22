"""Recorta a grande lanterna de bronze da ilustração Gemini do dono como objeto RGBA separado.

Uso: python recortar_lanterna.py <Gemini_Generated_Image_kqodeykqodeykqod.jpg> <saida.png>

Só a lanterna (argola, tampa, telhado, vidro com a chama e o aro de bronze da base), sem o pedestal da pintura:
ela fica em cima da plataforma redonda do chão da Praça. O miolo da argola sai transparente.
"""
import sys

from PIL import Image, ImageDraw

OUTLINE = [(1015, 262), (1030, 245), (1105, 245), (1120, 262), (1120, 330), (1100, 345), (1120, 372), (1122, 400),
           (1160, 420), (1205, 432), (1207, 480), (1180, 500), (1180, 700), (1186, 712), (1062, 728), (945, 712),
           (950, 700), (950, 500), (920, 480), (920, 432), (965, 420), (1003, 400), (1005, 372), (1030, 345),
           (1015, 330)]
RING_HOLE = (1032, 260, 1102, 337)


def main(src, out):
    image = Image.open(src).convert("RGBA")
    alpha = Image.new("L", image.size, 0)
    draw = ImageDraw.Draw(alpha)
    draw.polygon(OUTLINE, fill=255)
    draw.ellipse(RING_HOLE, fill=0)
    image.putalpha(alpha)
    box = alpha.getbbox()
    image.crop(box).save(out)
    print("lanterna bbox", box)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
