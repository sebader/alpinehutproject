<template>
   <section>

      <p v-show="loading">Loading...</p>

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
                     <td>Hut added</td>
                     <td>{{ new Date(hut.added).toLocaleDateString() }}</td>
                  </tr>
                  <tr>
                     <td>Last updated</td>
                     <td>{{ new Date(hut.lastUpdated).toLocaleTimeString() }}</td>
                  </tr>
                  <tr>
                     <td>Website</td>
                     <td><a :href="`${hut.hutWebsite}`" target="_blank">{{ shortWebsiteUrl(hut.hutWebsite) }}</a></td>
                  </tr>
                  <tr>
                     <td><span v-if="!hut.enabled"><i>Online booking inactive</i></span></td>
                     <td><a :href="`${hut.link}`" target="_blank">Online booking</a></td>
                  </tr>
                  <tr>
                     <td>Coordinates</td>
                     <td>
                        <router-link v-if="hut.latitude != null && hut.longitude != null"
                           :to="{ name: 'mapPage', query: { hutId: hut.id } }">{{ hut.latitude }}/{{ hut.longitude }}
                        </router-link>
                     </td>
                  </tr>
               </table>
            </div>
            <div class="col-sm">
               <div style="height: 35vh;">
                  <l-map ref="map" v-model:zoom="zoom" :center="mapCenter" :minZoom="6" :maxZoom="17">
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
         <div class="row">
            <br />
            <div class="col">
               <table>
                  <thead>
                     <tr>
                        <th>Date</th>
                        <th>Beds</th>
                        <th>Type of accommodation</th>
                     </tr>
                  </thead>
                  <tbody>
                     <template v-for="av in hut.availability">
                        <tr v-for="(roomAv, iSub) in av.roomAvailabilities" :key="roomAv.bedCategory">
                           <td v-if="iSub === 0" :rowspan="av.roomAvailabilities.length">{{ new
                                 Date(av.date).toDateString("dddd, dd.MM.yyyy")
                           }}</td>
                           <td>{{ roomAv.freeBeds }} / {{ roomAv.totalBeds }}</td>
                           <td>{{ roomAv.bedCategory }}</td>
                        </tr>
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