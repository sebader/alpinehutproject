import { processErrorResponseAsync } from "../utils"

const API_ENDPOINT = window.API_URL;

export default class MapviewService {

   availabilityData = {};

   async getAllAvailabilityOnDate(dateFilter) {
      if (this.availabilityData[dateFilter] == null) {
         var res = await fetch(`${API_ENDPOINT}/availability/${dateFilter}`);

         if (res.ok) {
            this.availabilityData[dateFilter] = await res.json();
         }
         else if (res.status === 404) {
            return 0;
         }
         else {
            throw new Error(await processErrorResponseAsync(res));
         }
      }
      return this.availabilityData[dateFilter];
   }
}