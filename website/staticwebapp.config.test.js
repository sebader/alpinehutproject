import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

test("cross-origin map tiles receive the site referrer", async () => {
   const source = await readFile(new URL("./staticwebapp.config.json", import.meta.url), "utf8");
   const config = JSON.parse(source);

   assert.equal(config.globalHeaders?.["Referrer-Policy"], "strict-origin-when-cross-origin");
});
