from pathlib import Path
from docx import Document
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

p=Path(__file__).resolve().parent/"Программная_часть_lab4_z0.docx"
d=Document(p)
for par in d.paragraphs:
    if par.text.startswith(("Таблица П1", "Таблица П2")):
        par.paragraph_format.page_break_before=True
        par.paragraph_format.keep_with_next=True
for t in d.tables:
    for r in t.rows:
        pr=r._tr.get_or_add_trPr()
        if pr.find(qn("w:cantSplit")) is None:
            pr.append(OxmlElement("w:cantSplit"))
d.save(p)
