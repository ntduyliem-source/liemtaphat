// Optional localhost-only preview; the page can also be opened directly as a file.
const http = require("node:http");
const fs = require("node:fs");
const path = require("node:path");
const port = Number(process.env.LOCUS_SPACE_PORT || 4175);
const allowed = { "/": ["index.html", "text/html"], "/index.html": ["index.html", "text/html"], "/style.css": ["style.css", "text/css"], "/state-machine.js": ["state-machine.js", "text/javascript"], "/app.js": ["app.js", "text/javascript"], "/markers.html": ["markers.html", "text/html"], "/markers.css": ["markers.css", "text/css"], "/marker-state.js": ["marker-state.js", "text/javascript"], "/marker-app.js": ["marker-app.js", "text/javascript"] };
http.createServer((request, response) => {
  const pathname = new URL(request.url, "http://127.0.0.1").pathname;
  const asset = allowed[pathname];
  if (!asset || request.method !== "GET") { response.writeHead(404); response.end("Not found"); return; }
  response.writeHead(200, { "Content-Type": asset[1] + "; charset=utf-8", "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff" });
  fs.createReadStream(path.join(__dirname, asset[0])).pipe(response);
}).listen(port, "127.0.0.1", () => process.stdout.write(`Locus M0 UX preview: http://127.0.0.1:${port}\n`));
