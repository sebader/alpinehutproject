import { processErrorResponseAsync } from "../utils"

const API_ENDPOINT = window.API_URL;

export default class BedCategoryService {

   bedCategories = null;

   async getAllBedCategories() {
      if (this.bedCategories == null) {
         var res = await fetch(`${API_ENDPOINT}/bedcategory`);

         if (res.ok) {
            this.bedCategories = await res.json();
         }
         else if (res.status === 404) {
            return 0;
         }
         else {
            throw new Error(await processErrorResponseAsync(res));
         }
      }
      return this.bedCategories;
   }

}