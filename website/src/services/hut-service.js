import { processErrorResponseAsync } from "../utils"
import { Constants } from "../utils";

const DATA_API_ENDPOINT = window.DATA_API_URL;

export default class HutService {

   huts = null;

   async listHutsAsync() {
      if (this.huts == null) {
         var res = await fetch(`${DATA_API_ENDPOINT}/Hut?$first=5000`);
         if (res.ok) {
            var result = await res.json();

            // Workaround to camelCase the property names
            for (let i = 0; i < result.value.length; i++) {
               let obj = result.value[i];
               for (let prop in obj) {
                  if (prop[0] === prop[0].toUpperCase()) {
                     obj[prop[0].toLowerCase() + prop.slice(1)] = obj[prop];
                     delete obj[prop];
                  }
               }
            }
            this.huts = result.value;
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
         var result = await res.json();
         if (result.value.length == 1) {

            // Workaround to camelCase the property names
            let obj = result.value[0];
            for (let prop in obj) {
               if (prop[0] === prop[0].toUpperCase()) {
                  obj[prop[0].toLowerCase() + prop.slice(1)] = obj[prop];
                  delete obj[prop];
               }
            }
            return obj;
         }
         throw new Error("Hut not found");
      }
      else {
         throw new Error(await processErrorResponseAsync(res));
      }
   }
}
