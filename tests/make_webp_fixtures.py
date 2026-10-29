from pathlib import Path
from PIL import Image, ImageDraw

base = Path(__file__).resolve().parent
root = base / 'WebP 測試 & 空格'
nested = root / '子資料夾' / '孫資料夾'
nested.mkdir(parents=True, exist_ok=True)
refs = base / 'webp-reference'
refs.mkdir(exist_ok=True)
size = (40, 30)
red = Image.new('RGBA', size, (255, 0, 0, 255))
green = Image.new('RGBA', size, (0, 255, 0, 255))
blue = Image.new('RGBA', size, (0, 0, 255, 255))
red.save(root / '01_靜態.WEBP', lossless=True)
transparent = Image.new('RGBA', size, (0, 0, 0, 0))
ImageDraw.Draw(transparent).rectangle((10, 5, 30, 25), fill=(40, 120, 200, 128))
transparent.save(root / '02_透明.webp', lossless=True)
red.save(nested / '03_動畫.webp', save_all=True, append_images=[green, blue], duration=[100, 250, 150], loop=0, lossless=True)
red.save(root / '04_有限循環.webp', save_all=True, append_images=[green, blue], duration=[100, 250, 150], loop=1, lossless=True)
Image.new('RGB', size, (220, 40, 70)).save(root / '05_有損.webp', quality=80)
(root / '06_損壞.webp').write_bytes(b'not a WebP image')
red.save(root / '07_零延遲.webp', save_all=True, append_images=[blue], duration=[0, 0], loop=0, lossless=True)
frames = []
for x, color in [(0, (255, 0, 0, 255)), (10, (0, 255, 0, 160)), (25, (0, 0, 255, 255))]:
    frame = Image.new('RGBA', size, (0, 0, 0, 0))
    ImageDraw.Draw(frame).rectangle((x, 5, x + 9, 14), fill=color)
    frames.append(frame)
alpha_animation = root / '08_透明動畫.webp'
frames[0].save(alpha_animation, save_all=True, append_images=frames[1:], duration=[100, 100, 100], loop=0, lossless=True)
with Image.open(alpha_animation) as image:
    for i in range(image.n_frames):
        image.seek(i)
        (refs / f'alpha-{i}.rgba').write_bytes(image.convert('RGBA').tobytes())
print(root)
