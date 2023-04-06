import { processErrorResponseAsync } from "../utils"

const DATA_API_ENDPOINT = window.DATA_API_URL;

export default class BedCategoryService {

   bedCategories = null;

   async getAllBedCategories() {
      if (this.bedCategories == null) {
         var res = await fetch(`${DATA_API_ENDPOINT}/BedCategory`);

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
            this.bedCategories = result.value;
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