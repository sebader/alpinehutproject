import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

test("OpenStreetMap uses the canonical tile endpoint", () => {
   return readFile(new URL("./mapview-service.js", import.meta.url), "utf8").then((source) => {
      const openStreetMapUrl = source.match(
         /name: "OpenStreetMap"[\s\S]*?url: "(https:\/\/[^"]+)"/,
      )?.[1];

      assert.equal(openStreetMapUrl, "https://tile.openstreetmap.org/{z}/{x}/{y}.png");
   });
});
