import { processErrorResponseAsync } from "../utils"
import { Constants } from "../utils";

const API_ENDPOINT = window.API_URL;
const DATA_API_ENDPOINT = window.DATA_API_URL;

export default class HutService {

   huts = null;

   async listHutsAsync() {
      if (this.huts == null) {
         var res = await fetch(`${DATA_API_ENDPOINT}/Hut?$first=5000`);
         if (res.ok) {
            this.huts = res.json().value;
         }
         else {
            throw new Error(await processErrorResponseAsync(res));
         }
      }
      return this.huts;
   }

   async getHutFromList(hutId) {
      if (this.huts == null) {
         await this.listHutsAsync();
      }
      return this.huts.find(hut => hut.id === hutId);
   }

   async getHutByIdAsync(hutId) {

      var res = await fetch(`${DATA_API_ENDPOINT}/Hut/Id/${hutId}`);

      if (res.ok) {
         var value = await res.json().value;
         if (value.length == 1) {
            return value[0];
         }
         return new Error("Hut not found");
      }
      else {
         throw new Error(await processErrorResponseAsync(res));
      }
   }
}
