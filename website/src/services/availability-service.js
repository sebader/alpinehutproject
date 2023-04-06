import { processErrorResponseAsync } from "../utils"

const API_ENDPOINT = window.API_URL;

export default class AvailabilityService {

   async getAvailabilityForHut(hutId) {
      var res = await fetch(`${API_ENDPOINT}/Hut/${hutId}/Availability`);

      if (res.ok) {
         return await res.json();
      }
      else if (res.status === 404) {
         return 0;
      }
      else {
         throw new Error(await processErrorResponseAsync(res));
      }
   }
}