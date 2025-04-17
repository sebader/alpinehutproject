<template>
   <section>

      <p v-show="loading">{{ $t('message.loading') }}...</p>

      <div v-if="!loading && hut != null">
         <div class="row">
            <div class="col-sm">
               <h1>{{ hut.name }}</h1>
               <table>
                  <tr>
                     <td>ID</td>
                     <td>{{ hut.id }}</td>
                  </tr>
                  <tr>
                     <td>{{ $t('message.hutAdded') }}</td>
                     <td>{{ new Date(hut.added).toLocaleDateString() }}</td>
                  </tr>
                  <tr>
                     <td>{{ $t('message.lastUpdated') }}</td>
                     <td>{{ new Date(hut.lastUpdated).toLocaleString() }}</td>
                  </tr>
                  <tr>
                     <td>Website</td>
                     <td><a :href="`${hut.hutWebsite}`" target="_blank">{{ shortWebsiteUrl(hut.hutWebsite) }}</a></td>
                  </tr>
                  <tr>
                     <td></td>
                     <td v-if="hut.enabled"><a :href="`${hut.link}`" target="_blank">{{ $t('message.onlineBooking')
                           }}</a></td>
                     <td v-else><a :href="`${hut.link}`" target="_blank"><i>{{ $t('message.onlineBookingInactive')
                              }}</i></a></td>
                  </tr>
                  <tr>
                     <td>{{ $t('message.country') }} / {{ $t('message.region') }}</td>
                     <td><span>{{ hut.country }}</span><span v-if="hut.region != null"> - {{ hut.region }}</span></td>
                  </tr>
                  <tr>
                     <td>{{ $t('message.coordinates') }}</td>
                     <td>
                        <router-link v-if="hut.latitude != null && hut.longitude != null"
                           :to="{ name: 'mapPage', query: { hutId: hut.id } }" :title="$t('message.showOnMap')">{{
                              hut.latitude }}/{{
                              hut.longitude
                           }}
                        </router-link>
                     </td>
                  </tr>
                  <tr v-if="hut.altitude != null">
                     <td>{{ $t('message.altitude') }}</td>
                     <td>{{ hut.altitude }}m</td>
                  </tr>
               </table>
            </div>
            <div class="col-sm">
               <div style="height: 35vh;">
                    <l-map v-if="hut.latitude != null && hut.longitude != null" ref="map" v-model:zoom="zoom" :center="mapCenter" :minZoom="6" :maxZoom="17">
                     <l-control-layers position="topright"></l-control-layers>
                     <l-tile-layer v-for="tileProvider in tileProviders" :key="tileProvider.name"
                        :name="tileProvider.name" :visible="tileProvider.visible" :url="tileProvider.url"
                        :attribution="tileProvider.attribution" layer-type="base" />
                     <l-marker ref="markerItems" :name="hut.name" :lat-lng="[hut.latitude, hut.longitude]"
                        :icon="markerIcon">
                        <l-tooltip>
                           <b>{{ hut.name }}</b>
                        </l-tooltip>
                     </l-marker>
                  </l-map>
               </div>
            </div>
         </div>
         <div class="row" v-if="hut.enabled">
            <br />
            <div class="col">
               <div class="weekday-filter mb-3">
                  <label for="weekday-select">{{ $t('message.filterByWeekdays') || 'Filter by Weekdays' }}:</label>
                  <div class="weekday-checkboxes">
                     <div v-for="weekday in weekdays" :key="weekday" class="weekday-option">
                        <input type="checkbox" :id="weekday" :value="weekday" v-model="selectedWeekdays">
                        <label :for="weekday">{{ weekday }}</label>
                     </div>
                  </div>
               </div>
               <table>
                  <thead>
                     <tr>
                        <th>{{ $t('message.date') }}</th>
                        <th>{{ $t('message.beds') }}</th>
                        <th>{{ $t('message.typeOfAccommodation') }}</th>
                     </tr>
                  </thead>
                  <tbody>
                     <template v-for="month in this.availabilityByMonth" :key="month">
                        <tr @click="toggleCollapse(month)" style="cursor: pointer;">
                           <th colspan="3">{{ month.month }} <span v-if="month.collapsed">▼</span><span v-else>▲</span>
                           </th>
                        </tr>
                        <template v-if="!month.collapsed" v-for="av in month.availabilities">
                           <tr v-for="(roomAv, iSub) in av.roomAvailabilities" :key="roomAv.bedCategory">
                              <td v-if="iSub === 0" :rowspan="av.roomAvailabilities.length">{{ new
                                 Date(av.date).toDateString() }}</td>
                              <td :class="getBedAvailabilityClass(roomAv.freeBeds, roomAv.totalBeds)">{{ roomAv.freeBeds }} / {{ roomAv.totalBeds }}</td>
                              <td :class="getBedAvailabilityClass(roomAv.freeBeds, roomAv.totalBeds)">{{ roomAv.bedCategory }}</td>
                           </tr>
                           <tr v-if="av.hutClosed">
                              <td>{{ new Date(av.date).toDateString() }}</td>
                              <td colspan="2">{{ $t('message.hutClosed') }}</td>
                           </tr>
                        </template>
                     </template>
                  </tbody>
               </table>
            </div>
         </div>
      </div>
   </section>
</template>

<style scoped>
.availability-box {
   border: 2px dashed rgb(233, 233, 233);
   padding: 8px;
}

.availability-box .name {
   font-size: 9pt
}

.weekday-filter {
   margin-top: 10px;
}

.weekday-checkboxes {
   display: flex;
   flex-wrap: wrap;
   gap: 10px;
   margin-top: 5px;
}

.weekday-option {
   display: flex;
   align-items: center;
   gap: 5px;
}

.bed-availability-high {
  background-color: rgba(75, 192, 75, 0.2);
  color: #2c882c;
}

.bed-availability-medium {
  background-color: rgba(255, 159, 64, 0.2);
  color: #b36c00;
}

.bed-availability-low {
  background-color: rgba(255, 99, 132, 0.2);
  color: #c92432;
}

table {
  border-collapse: collapse;
  width: 100%;
}

table tr:hover {
  background-color: rgba(0, 0, 0, 0.05);
}
</style>

<script>
import { shortWebsiteUrl } from "../utils"
import { Constants } from '../utils';
import { EventBus } from "../main"
import { tileProviders } from "../services/mapview-service";

import L from 'leaflet';
import {
   LMap,
   LIcon,
   LTileLayer,
   LMarker,
   LControlLayers,
   LTooltip,
} from "@vue-leaflet/vue-leaflet";
import "leaflet/dist/leaflet.css";

export default {
   components: {
      LMap,
      LIcon,
      LTileLayer,
      LMarker,
      LControlLayers,
      LTooltip,
   },
   data: function () {
      return {
         hut: null,
         availabilityByMonth: [],
         loading: false,
         mapCenter: null,
         zoom: 10,
         tileProviders,
         markerIcon: L.icon({
            iconUrl: 'https://cdn.rawgit.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-blue.png',
            iconSize: [25, 41],
            iconAnchor: [12, 41],
            popupAnchor: [1, -34]
         }),
         weekdays: ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'],
         selectedWeekdays: [],
         originalAvailability: [],
      }
   },
   watch: {
      selectedWeekdays: {
         handler() {
            if (this.hut && this.originalAvailability.length > 0) {
               this.availabilityByMonth = this.groupByMonth(this.originalAvailability);
            }
         },
         deep: true
      }
   },
   methods: {
      shortWebsiteUrl(url) {
         return shortWebsiteUrl(url);
      },
      toggleCollapse(month) {
         month.collapsed = !month.collapsed;
      },
      filterByWeekdays(availabilities) {
         if (this.selectedWeekdays.length === 0) return availabilities;
         return availabilities.filter(av => this.selectedWeekdays.includes(new Date(av.date).toLocaleString('default', { weekday: 'long' })));
      },
      groupByMonth(availabilities) {
         const filteredAvailabilities = this.filterByWeekdays(availabilities);
         const months = {};
         filteredAvailabilities.forEach(av => {
            const month = new Date(av.date).toLocaleString('default', { month: 'long', year: 'numeric' });
            if (!months[month]) {
               months[month] = { month, availabilities: [], collapsed: true };
            }
            months[month].availabilities.push(av);
            // current month is always expanded
            months[month].collapsed = new Date(av.date).getMonth() !== new Date().getMonth() || new Date(av.date).getFullYear() !== new Date().getFullYear();
         });
         return Object.values(months);
      },
      getBedAvailabilityClass(freeBeds, totalBeds) {
         // No beds available
         if (freeBeds === 0) {
            return 'bed-availability-low';
         }
         
         // At least 4 beds or 10% of total beds are available
         if (freeBeds >= 4 || (freeBeds / totalBeds) >= 0.1) {
            return 'bed-availability-high';
         }
         
         // Otherwise (between 1-3 beds and less than 10%)
         return 'bed-availability-medium';
      },
   },
   async created() {
      this.loading = true;
      try {
         const hutId = this.$route.params.hutId;
         if (hutId == null) {
            EventBus.$emit(Constants.EVENT_ERROR, "Hut ID is required.");
         }
         else {
            this.hut = await this.$HutService.getHutByIdAsync(hutId);
            this.hut.availability = await this.$AvailabilityService.getAvailabilityForHut(this.hut.id);
            this.originalAvailability = [...this.hut.availability]; // Store original data
            this.availabilityByMonth = this.groupByMonth(this.hut.availability);
            this.mapCenter = [this.hut.latitude, this.hut.longitude];
         }
      }
      catch (e) {
         EventBus.$emit(Constants.EVENT_ERROR, "There was a problem fetching the hut. " + e.message);
      }

      this.loading = false;
   }
}
</script>
