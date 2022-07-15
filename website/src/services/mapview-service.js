import { processErrorResponseAsync } from "../utils"

const API_ENDPOINT = window.API_URL;

export default class MapviewService {

   async getAllAvailabilityOnDate(dateFilter) {
      var res = await fetch(`${API_ENDPOINT}/availability/${dateFilter}`);

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