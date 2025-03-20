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
                              <td>{{ roomAv.freeBeds }} / {{ roomAv.totalBeds }}</td>
                              <td>{{ roomAv.bedCategory }}</td>
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
</style>

<script>
import { shortWebsiteUrl } from "../utils"
import Vue from 'vue'
import { Constants } from '../utils';
import { EventBus } from "../main"

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
         tileProviders: [
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
         ],
         markerIcon: L.icon({
            iconUrl: 'https://cdn.rawgit.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-blue.png',
            iconSize: [25, 41],
            iconAnchor: [12, 41],
            popupAnchor: [1, -34]
         })
      }
   },
   methods: {
      shortWebsiteUrl(url) {
         return shortWebsiteUrl(url);
      },
      toggleCollapse(month) {
         month.collapsed = !month.collapsed;
      },
      groupByMonth(availabilities) {
         const months = {};
         availabilities.forEach(av => {
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