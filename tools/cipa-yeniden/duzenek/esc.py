import sys
p = sys.argv[1]
q = p.replace(chr(92), "/").replace(":", chr(92) + ":")
print("'" + q + "'")
