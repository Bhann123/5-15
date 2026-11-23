"""Creates example parent-answer workbooks in samples/: a filled-in example and a blank template.

Answers go in column E from E2 downwards (question 1-181), which is what the app reads.
"""
import random, re
import openpyxl
from openpyxl.styles import Alignment, Font, PatternFill
from openpyxl.worksheet.datavalidation import DataValidation

OPTS = ["Á ekki við", "Á stundum/ að nokkru leyti við", "Á við"]
texts = re.findall(r'^\s+"(.*)",$', open("src/FimmFimmtan.Core/ItemTexts.cs", encoding="utf-8").read(), re.M)
assert len(texts) == 181

SECTIONS = [(1, "Grófhreyfingar"), (8, "Fínhreyfingar"), (18, "Athygli og einbeiting"), (27, "Ofvirkni og hvatvísi"),
            (36, "Vanvirkni og drifleysi"), (40, "Skipulag og áætlanagerð"), (43, "Skynjun á rými og áttum"),
            (48, "Tímaskyn"), (52, "Skynjun eigin líkama"), (57, "Skynjun forma og mynda"), (61, "Minni"),
            (72, "Skilningur á mæltu máli"), (77, "Máltjáning"), (90, "Tjáskipti"), (93, "Lestur og skrift"),
            (101, "Reikningur"), (106, "Almenn námsgeta"), (112, "Að takast á við nám"), (122, "Félagsfærni"),
            (149, "Innhverf einkenni"), (161, "Úthverf hegðun"), (174, "Áráttur, þráhyggja og kippir")]


def section(i):
    return [name for start, name in SECTIONS if start <= i][-1]


def workbook(answers):
    wb = openpyxl.Workbook()
    ws = wb.active
    ws.title = "Svör"
    for col, h in zip("ABCDE", ["Nr.", "Spurning", "Svið", "", "Svar"]):
        ws[f"{col}1"] = h
        ws[f"{col}1"].font = Font(bold=True, color="FFFFFF")
        ws[f"{col}1"].fill = PatternFill("solid", fgColor="0F3D5E")
    dv = DataValidation(type="list", formula1='"' + ",".join(OPTS) + '"', allow_blank=True)
    ws.add_data_validation(dv)
    for i, t in enumerate(texts, start=1):
        ws.cell(i + 1, 1, i)
        ws.cell(i + 1, 2, t).alignment = Alignment(wrap_text=True, vertical="top")
        ws.cell(i + 1, 3, section(i))
        e = ws.cell(i + 1, 5, answers[i - 1] if answers else None)
        e.fill = PatternFill("solid", fgColor="FFF8E1")
        dv.add(e)
    ws.column_dimensions["A"].width = 6
    ws.column_dimensions["B"].width = 80
    ws.column_dimensions["C"].width = 26
    ws.column_dimensions["D"].width = 2
    ws.column_dimensions["E"].width = 32
    ws.freeze_panes = "A2"
    return wb


# Example: a 9-year-old with marked attention/hyperactivity and some acting-out problems, otherwise typical.
random.seed(515)
weights = {
    "Athygli og einbeiting": [0.1, 0.45, 0.45], "Ofvirkni og hvatvísi": [0.15, 0.45, 0.4],
    "Skipulag og áætlanagerð": [0.2, 0.5, 0.3], "Úthverf hegðun": [0.45, 0.4, 0.15],
    "Að takast á við nám": [0.35, 0.45, 0.2], "Minni": [0.6, 0.35, 0.05],
}
answers = [random.choices(OPTS, weights.get(section(i), [0.85, 0.13, 0.02]))[0] for i in range(1, 182)]
workbook(answers).save("samples/daemi_svor.xlsx")
workbook(None).save("samples/snidmat_svor.xlsx")
print("ok")
