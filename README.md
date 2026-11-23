# 5-15 Skorun

Windows-forrit sem skorar **5-15** (Norrænn spurningalisti fyrir foreldra um þroska barns og hegðun, FTF) og ber
niðurstöðurnar saman við viðmið eftir aldri og kyni.

**Sækja forritið:** opnaðu [Releases](https://github.com/Bhann123/5-15/releases/latest), sæktu
`5-15-Skorun-Windows.zip`, afþjappaðu og keyrðu `5-15 Skorun.exe`. Ekkert þarf að setja upp. Ef Windows varar við
skránni: „More info“ → „Run anyway“. Ný útgáfa er smíðuð sjálfkrafa við hverja breytingu á `main`.

1. Sláðu inn nafn, aldur (5–17 ára) og kyn barns.
2. Hladdu inn Excel-skrá (.xlsx) með svörum foreldra – með hnappnum eða með því að draga skrána inn í gluggann –
   eða smelltu á **Svara í forritinu** og svaraðu í flipanum „Svör“. Þar má smella á svar eða ýta á 0, 1 eða 2
   (bendillinn færist þá á næstu spurningu; örvar færa, Delete hreinsar). Innlesnum svörum má líka breyta.
   **Vista svör…** vistar svörin (og nafn, aldur og kyn) sem Excel-skrá á sama sniði, sem má hlaða inn aftur.
3. Niðurstöður birtast strax: niðurstaða hvers sviðs og undirsviðs, viðmið (miðgildi, meðaltal, staðalfrávik,
   90% og 98%), myndrænt snið og mat. Vista má skýrslu sem PDF eða Excel.

## Excel-skráin

Svörin byrja í reit **E2** og halda áfram niður dálk E, eitt svar í línu, í röð spurninga 1–181
(`samples/daemi_svor.xlsx` er útfyllt dæmi og `samples/snidmat_svor.xlsx` autt sniðmát með fellilistum í dálki E). Leyfileg svör:

| Svar | Stig |
| --- | --- |
| Á ekki við | 0 |
| Á stundum/ að nokkru leyti við | 1 |
| Á við | 2 |

Einnig má nota 0, 1 og 2. Tóm reitur og „óviðkomandi“ teljast ósvaruð. Hástafir, bil og broddstafir skipta ekki máli.
Svör við spurningunum „Hamla erfiðleikar … í daglegu lífi“ (alls ekki / lítið / mikið / mjög mikið) eru þekkt og
hliðra ekki númerun ef þau eru í dálkinum. Ef svörin eru í línu 2 frá E til hægri (útflutningur úr Forms) er það
greint sjálfkrafa. Flipinn **Svör** sýnir hverja spurningu með svarinu og reitnum sem það kom úr.

Valfrjálst: ef **G1:G3** innihalda „Nafn“, „Aldur“ og „Kyn“ eru gildin í **H1:H3** lesin inn (kyn: Drengur/Stúlka).

## Hvernig er skorað

* Hver spurning gefur 0, 1 eða 2 stig.
* Niðurstaða undirsviðs = meðaltal svaraðra spurninga á undirsviðinu.
* Niðurstaða aðalsviðs = meðaltal **allra** spurninga á sviðinu (ekki meðaltal undirsviðanna).
* Niðurstaða við eða yfir 90. hundraðsmarki viðmiðunarhóps er merkt gul, við eða yfir 98. hundraðsmarki rauð.
  z-gildi = (niðurstaða − meðaltal) / staðalfrávik.
* Námssviðið (93–121) er ekki metið fyrir 5–7 ára (engin viðmið).
* Myndritin (flipinn **Myndrit** og síða 2 í PDF-skýrslunni) eru skipt í aðalþætti og undirþætti. Hvert svið er
  sýnt á eigin hundraðsmarkakvarða: 0 til 90. hundraðsmarks fyllir fyrri helming ássins, 90.–98. næsta fjórðung og
  98. hundraðsmark upp í 2,0 síðasta fjórðunginn, eins og í myndritunum í 5-15 skýrslunum.

| Svið | Spurningar | Undirsvið |
| --- | --- | --- |
| Hreyfifærni | 1–17 | Grófhreyfingar 1–7, Fínhreyfingar 8–17 |
| Stýrifærni | 18–42 | Athygli og einbeiting 18–26, Ofvirkni og hvatvísi 27–35, Vanvirkni og drifleysi 36–39, Skipulag og áætlanagerð 40–42 |
| Skynjun | 43–60 | Rými og áttir 43–47, Tímaskyn 48–51, Eigin líkami 52–56, Form og myndir 57–60 |
| Minni | 61–71 | |
| Mál og tal | 72–92 | Skilningur 72–76, Máltjáning 77–89, Tjáskipti 90–92 |
| Nám | 93–121 | Lestur og skrift 93–100, Reikningur 101–105, Almenn námsgeta 106–109, Að takast á við nám 112–121 |
| Félagsfærni | 122–148 | |
| Tilfinningar og hegðun | 149–181 | Innhverf einkenni 149–160, Úthverf hegðun 161–173, Áráttur/þráhyggja/kippir 174–181 |

Skiptingin var sannreynd með dæminu í viðmiðunartöflunum (`docs/norms/`): öll gildi í „Result“-dálkinum fást
nákvæmlega (t.d. Hreyfifærni 4/17 = 0,24, Stýrifærni 35/25 = 1,40, Tilfinningar og hegðun 6/33 = 0,18).
Spurningar 110 og 111 lýsa styrkleikum og eru ekki skoraðar. Það sést á niðurstöðum frá 5-15.org: Almenn námsgeta
tekur gildi í fjórðungum (4 spurningar) og Nám í 27-hlutum (27 spurningar).

## Bera saman við niðurstöður frá 5-15.org

Sæktu niðurstöðuskýrslur (PDF) úr aðgangi þínum á 5-15.org í eina möppu og keyrðu:

```bash
pip install pdfplumber
python3 tools/verify_reports.py <mappa>
```

Fyrir hverja skýrslu er athugað að viðmiðin passi nákvæmlega við eina af átta töflum forritsins og að hver
niðurstaða fáist með spurningaskiptingu forritsins (heil stig deilt með fjölda spurninga, aðalþáttur = vegið
meðaltal undirþátta).

## Keyra og smíða

Krefst [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/FimmFimmtan.App            # keyra forritið
dotnet test                                          # prófanir á skorun, innlestri og skýrslum
dotnet publish src/FimmFimmtan.App -c Release -r win-x64 -o publish/win-x64
```

Síðasta skipunin býr til eina sjálfstæða skrá, `publish/win-x64/5-15 Skorun.exe`, sem keyrir á Windows 10/11 (64-bita)
án þess að setja upp .NET. Hægt er að smíða hana á Windows, macOS eða Linux.

## Uppbygging

* `src/FimmFimmtan.Core` – skorun, viðmið, innlestur Excel og PDF/Excel-skýrslur (ClosedXML, QuestPDF).
  `NormData.cs` og `ItemTexts.cs` eru búnar til úr PDF-skjölunum með `tools/extract_norms.py` og `tools/extract_items.py`.
* `src/FimmFimmtan.App` – notendaviðmót (Avalonia).
* `tests/FimmFimmtan.Tests` – prófanir, m.a. að dæmið í viðmiðunartöflunum fáist rétt fyrir alla aldurshópa.

Listann má ekki nota einan og sér til að komast að greiningarniðurstöðu.
