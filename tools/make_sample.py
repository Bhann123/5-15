"""Creates example parent-answer workbooks in samples/: a filled-in example and a blank template.

Same layout as the 5-15.org export (id, type, number, label, answer): answers 0-2 in column E, the ten
"Hamla ... í daglegu lífi?" questions (0-3) on their own rows, and a text row before question 93.
"""
import random, re
import openpyxl
from openpyxl.styles import Font, PatternFill
from openpyxl.worksheet.datavalidation import DataValidation

texts = re.findall(r'^\s+"(.*)",$', open("src/FimmFimmtan.Core/ItemTexts.cs", encoding="utf-8").read(), re.M)
assert len(texts) == 181
impact = {int(after): (int(num), text) for num, after, text in
          re.findall(r'new\((\d+), (\d+), "\w+", "(.*?)",', open("src/FimmFimmtan.Core/ImpactQuestions.cs", encoding="utf-8").read())}
assert len(impact) == 10
NOTE = "Ef barnið er yngra en 8 ára vinsamlegast svarið spurningu 131 næst:"
LABELS = ["Á ekki við", "Á stundum við", "Á við"]
IMPACT_LABELS = ["Alls ekki", "Lítið", "Mikið", "Mjög mikið"]

SECTIONS = [(1, "Grófhreyfingar"), (8, "Fínhreyfingar"), (18, "Athygli og einbeiting"), (27, "Ofvirkni og hvatvísi"),
            (36, "Vanvirkni og drifleysi"), (40, "Skipulag og áætlanagerð"), (43, "Skynjun á rými og áttum"),
            (48, "Tímaskyn"), (52, "Skynjun eigin líkama"), (57, "Skynjun forma og mynda"), (61, "Minni"),
            (72, "Skilningur á mæltu máli"), (77, "Máltjáning"), (90, "Tjáskipti"), (93, "Lestur og skrift"),
            (101, "Reikningur"), (106, "Almenn námsgeta"), (112, "Að takast á við nám"), (122, "Félagsfærni"),
            (149, "Innhverf einkenni"), (161, "Úthverf hegðun"), (174, "Áráttur, þráhyggja og kippir")]


def section(i):
    return [name for start, name in SECTIONS if start <= i][-1]


def workbook(answers=None, impact_answers=None, child=None):
    wb = openpyxl.Workbook()
    ws = wb.active
    ws.title = "5-15 Norrænn spurningalisti"
    for col, h in enumerate(["id", "type", "number", "label", "answer", "svar"], start=1):
        ws.cell(1, col, h).font = Font(bold=True)
    items_dv = DataValidation(type="list", formula1='"0,1,2"', allow_blank=True)
    impact_dv = DataValidation(type="list", formula1='"0,1,2,3"', allow_blank=True)
    ws.add_data_validation(items_dv)
    ws.add_data_validation(impact_dv)
    answer_fill = PatternFill("solid", fgColor="FFF8E1")
    impact_fill = PatternFill("solid", fgColor="EEF4FA")

    row = 1

    def add(id_, type_, label):
        nonlocal row
        row += 1
        for col, v in enumerate([id_, type_, row - 1, label], start=1):
            ws.cell(row, col, v)

    for i, text in enumerate(texts, start=1):
        if i == 93:
            add("", "textbox", NOTE)
            for col in range(1, 7):
                ws.cell(row, col).font = Font(color="64748B")
        add(f"q{i}", "checkbox", text)
        e = ws.cell(row, 5, answers[i - 1] if answers else None)
        e.fill = answer_fill
        items_dv.add(e)
        if answers:
            ws.cell(row, 6, LABELS[answers[i - 1]])
        if i in impact:
            num, q = impact[i]
            add(f"h{num}", "checkbox", q)
            e = ws.cell(row, 5, impact_answers[num - 1] if impact_answers else None)
            impact_dv.add(e)
            if impact_answers:
                ws.cell(row, 6, IMPACT_LABELS[impact_answers[num - 1]])
            for col in range(1, 7):
                ws.cell(row, col).fill = impact_fill
                ws.cell(row, col).font = Font(italic=True)

    note = ws.cell(1, 8, "Svör: 0 = Á ekki við, 1 = Á stundum við, 2 = Á við. "
                         "Hömlun í daglegu lífi: 0 = Alls ekki, 1 = Lítið, 2 = Mikið, 3 = Mjög mikið.")
    note.font = Font(italic=True, color="64748B")
    for r, (label, value) in enumerate(zip(["Nafn", "Aldur", "Kyn"], child or ("", "", "")), start=3):
        ws.cell(r, 8, label).font = Font(bold=True)
        ws.cell(r, 9, value if value != "" else None).fill = answer_fill
    sex = DataValidation(type="list", formula1='"Drengur,Stúlka"', allow_blank=True)
    ws.add_data_validation(sex)
    sex.add("I5")

    for col, width in zip("ABCDEFGHI", [6, 10, 8, 80, 8, 16, 2, 8, 22]):
        ws.column_dimensions[col].width = width
    ws.freeze_panes = "A2"
    return wb


# Example: a 9-year-old with marked attention/hyperactivity and some acting-out problems, otherwise typical.
random.seed(515)
weights = {
    "Athygli og einbeiting": [0.1, 0.45, 0.45], "Ofvirkni og hvatvísi": [0.15, 0.45, 0.4],
    "Skipulag og áætlanagerð": [0.2, 0.5, 0.3], "Úthverf hegðun": [0.45, 0.4, 0.15],
    "Að takast á við nám": [0.35, 0.45, 0.2], "Minni": [0.6, 0.35, 0.05],
}
answers = [random.choices([0, 1, 2], weights.get(section(i), [0.85, 0.13, 0.02]))[0] for i in range(1, 182)]
impact_answers = [0, 2, 1, 2, 0, 1, 0, 2, 1, 2]
workbook(answers, impact_answers, ("Dæmi Dæmason", 9, "Drengur")).save("samples/daemi_svor.xlsx")
workbook().save("samples/snidmat_svor.xlsx")
print("ok")
