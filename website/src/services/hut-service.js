import { processErrorResponseAsync } from "../utils"
import { Constants } from "../utils";

const API_ENDPOINT = window.API_URL;

export default class HutService {

   huts = null;

   async listHutsAsync() {
      if (this.huts == null) {
         var res = await fetch(`${API_ENDPOINT}/hut`);
         if (res.ok) {
            this.huts = res.json();
         }
         else {
            throw new Error(await processErrorResponseAsync(res));
         }
      }
      return this.huts;
   }

   async getHutFromList(hutId){
      if (this.huts == null) {
         await this.listHutsAsync();
      }
      return this.huts.find(hut => hut.id === hutId);
   }

   async getHutByIdAsync(hutId) {

      var res = await fetch(`${API_ENDPOINT}/hut/${hutId}`);

      if (res.ok) {
         return await res.json();
      }
      else {
         throw new Error(await processErrorResponseAsync(res));
      }
   }
}