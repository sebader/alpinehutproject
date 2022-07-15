import { processErrorResponseAsync } from "../utils"
import { Constants } from "../utils";

const API_ENDPOINT = window.API_URL;

export default class HutService {

   async listHutsAsync() {
      var res = await fetch(`${API_ENDPOINT}/hut`);
      if (res.ok) {
         return res.json();
      }
      else {
         throw new Error(await processErrorResponseAsync(res));
      }
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
