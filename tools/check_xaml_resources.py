"""Fails if a XAML file references a StaticResource/DynamicResource key that no dictionary defines.
WPF only reports a missing StaticResource at runtime (as a crash), so CI runs this instead."""
import re, sys, pathlib
root = pathlib.Path(__file__).resolve().parent.parent / "src" / "Downora.App"
files = list(root.rglob("*.xaml"))
defined = set()
for f in files:
    defined |= set(re.findall(r'x:Key="([^"]+)"', f.read_text(encoding="utf-8")))
problems = []
for f in files:
    text = f.read_text(encoding="utf-8")
    for kind, key in re.findall(r'\{(StaticResource|DynamicResource) ([A-Za-z0-9_.]+)\}', text):
        if key not in defined:
            problems.append(f"{f.relative_to(root)}: {kind} {key}")
    # Each dictionary/view must not use a StaticResource from a file it can't see at load time is checked by merge structure.
# Type check for the one place WPF needs a GridLength: Column/Row definitions fed from a resource.
types = {}
for f in files:
    for tag, key in re.findall(r'<(?:\w+:)?(\w+) x:Key="([^"]+)"', f.read_text(encoding="utf-8")):
        types[key] = tag
for f in files:
    for key in re.findall(r'<(?:ColumnDefinition|RowDefinition)[^>]*(?:Width|Height)="\{StaticResource ([A-Za-z0-9_.]+)\}"', f.read_text(encoding="utf-8")):
        if types.get(key) != "GridLength":
            problems.append(f"{f.relative_to(root)}: {key} is a {types.get(key)}, but a column/row size needs a GridLength")
palettes = {}
for name in ("Palette.Dark.xaml", "Palette.Light.xaml"):
    palettes[name] = set(re.findall(r'x:Key="([^"]+)"', (root / "Resources/Theme" / name).read_text(encoding="utf-8")))
if palettes["Palette.Dark.xaml"] != palettes["Palette.Light.xaml"]:
    problems.append("Palette.Dark.xaml and Palette.Light.xaml define different keys")
if problems:
    print("\n".join(problems)); sys.exit(1)
print(f"OK: {len(files)} XAML files, {len(defined)} keys")
