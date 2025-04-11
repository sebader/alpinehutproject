import { processErrorResponseAsync } from "../utils"
import { Constants } from "../utils";

const API_ENDPOINT = window.API_URL;

export default class HutService {

   huts = null;

   async listHutsAsync(forceRefresh = false) {
      if (this.huts == null || forceRefresh) {
         const url = forceRefresh ? 
            `${API_ENDPOINT}/huts?_=${Date.now()}` : 
            `${API_ENDPOINT}/huts`;
         var res = await fetch(url);
         if (res.ok) {
            var result = await res.json();
            this.huts = result;
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
      var res = await fetch(`${API_ENDPOINT}/huts/${hutId}`);

      if (res.ok) {
         return await res.json();
      }
      else {
         throw new Error(await processErrorResponseAsync(res));
      }
   }

   async updateHutAsync(hut) {
      const res = await fetch(`${API_ENDPOINT}/huts/${hut.id}`, {
         method: 'PUT',
         headers: {
            'Content-Type': 'application/json',
         },
         body: JSON.stringify(hut)
      });

      if (res.ok) {
         const result = await res.json();
         return result;
      }
      else {
         throw new Error(await processErrorResponseAsync(res));
      }
   }

   async deleteHutAsync(hutId) {
      const res = await fetch(`${API_ENDPOINT}/huts/${hutId}`, {
         method: 'DELETE'
      });

      if (res.ok) {
         const result = await res.json();
         return result;
      }
      else {
         throw new Error(await processErrorResponseAsync(res));
      }
   }
}
