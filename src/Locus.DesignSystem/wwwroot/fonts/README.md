# Studio fonts

Locally served WOFF2 variable fonts from these pinned npm packages (downloaded 2026-10-03):

- `@fontsource-variable/newsreader@5.3.0`, standard normal (optical size + weight).
- `@fontsource-variable/inter@5.3.0`, weight normal.
- `@fontsource-variable/jetbrains-mono@5.3.0`, weight normal.

Keep Latin, Latin Extended and Vietnamese subsets for each family. `fonts.css` retains the packages' original family names, weight ranges, unicode ranges and `font-display: swap`; only the relative file paths are changed. All three families are OFL-1.1; full notices are the adjacent `*-OFL.txt` files. They are UI fonts; the formula renderer and exported math assets remain unchanged.

To refresh deliberately: `npm pack @fontsource-variable/<family>@<version> --ignore-scripts`, extract the package, copy the matching `files/*-{latin,latin-ext,vietnamese}-*-normal.woff2` and license, and update the corresponding declarations and version record here. Do not replace these with CDN imports; the Web and Desktop builds must work offline.
