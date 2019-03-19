import os

DOCS_PATH = "./docs"
REPAIR_DOCS_PATH = "./repair-docs"

os.mkdir(REPAIR_DOCS_PATH)

for docname in os.listdir("./docs"):
    if "Images" == docname:
        continue

    lines = open(f"{DOCS_PATH}/{docname}", 'r').readlines()
    new_doc = open(f"{REPAIR_DOCS_PATH}/{docname}", "w")
    flag = False

    for index, line in enumerate(lines):
        if line[4:7] == ":::":
            line = f"``` {line.strip()[3:]}\n"
            flag = True

        elif flag and ("\n" == line and "    " != lines[index+1][:4]):
            line = '```\n\n'
            flag = False

        elif flag:
            line = line[4:]

        new_doc.write(line)


# os.rmdir(DOCS_PATH)
# os.rename(REPAIR_DOCS_PATH, DOCS_PATH)
