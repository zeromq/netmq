import os

DOCS_PATH = "./docs"
REPAIR_DOCS_PATH = "./repair-docs"
KEYWORD_LINE = "\t:::csharp"

os.mkdir(REPAIR_DOCS_PATH)

for docname in os.listdir("./docs"):
    lines = open(f"{DOCS_PATH}/{docname}", 'r').readlines()
    new_doc = open(f"{REPAIR_DOCS_PATH}/{docname}", "w")
    flag = False

    for line in lines:
        if KEYWORD_LINE == line:
            line = "``` csharp"
            flag = True

        if flag and "" == line:
            line = '```'
            flag = False

        new_doc.write(line+'\n')


os.rmdir(DOCS_PATH)
os.rename(REPAIR_DOCS_PATH, DOCS_PATH)
