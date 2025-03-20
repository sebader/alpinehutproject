import { processErrorResponseAsync } from "../utils"

const API_ENDPOINT = window.API_URL;

/**
 * Map tile providers configuration for use across the application
 */
export const tileProviders = [
   {
      name: 'OpenStreetMap',
      visible: true,
      minZoom: 6,
      attribution:
         '&copy; <a target="_blank" href="http://osm.org/copyright">OpenStreetMap</a> contributors',
      url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
   },
   {
      name: 'OpenTopoMap',
      visible: false,
      url: 'https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png',
      minZoom: 6,
      maxZoom: 17,
      attribution:
         'Map data: &copy; <a href="http://www.openstreetmap.org/copyright">OpenStreetMap</a>, <a href="http://viewfinderpanoramas.org">SRTM</a> | Map style: &copy; <a href="https://opentopomap.org">OpenTopoMap</a> (<a href="https://creativecommons.org/licenses/by-sa/3.0/">CC-BY-SA</a>)',
   },
   {
      name: 'TracesTrack',
      visible: false,
      url: 'https://tile.tracestrack.com/topo__/{z}/{x}/{y}.png?key=366d03ac32a75030ef201d32a2f995fc',
      minZoom: 6,
      maxZoom: 17,
      attribution:
         '© <a href="https://www.openstreetmap.org/copyright" target="_blank">OpenStreetMap</a> contributors. Tiles courtesy of <a href="https://www.tracestrack.com/" target="_blank">Tracestrack Maps</a>',
   }
];

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
