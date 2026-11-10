"""Creates samples/daemi_svor.xlsx: a filled-in example with answers in E2 downwards."""
import random, re
import openpyxl
from openpyxl.styles import Font

texts = re.findall(r'^\s+"(.*)",$', open("src/FimmFimmtan.Core/ItemTexts.cs", encoding="utf-8").read(), re.M)
assert len(texts) == 181
opts = ["Á ekki við", "Á stundum/ að nokkru leyti við", "Á við"]
random.seed(515)
wb = openpyxl.Workbook(); ws = wb.active; ws.title = "Svör"
for col, h in zip("ABCDE", ["Nr.", "Spurning", "", "", "Svar"]):
    ws[f"{col}1"] = h; ws[f"{col}1"].font = Font(bold=True)
for i, t in enumerate(texts, start=1):
    high = 18 <= i <= 35 or 161 <= i <= 167
    w = [0.25, 0.4, 0.35] if high else [0.8, 0.17, 0.03]
    ws.cell(i + 1, 1, i); ws.cell(i + 1, 2, t)
    ws.cell(i + 1, 5, random.choices(opts, w)[0])
ws.column_dimensions["B"].width = 90; ws.column_dimensions["E"].width = 32
wb.save("samples/daemi_svor.xlsx")
print("ok")
