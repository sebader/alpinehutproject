<template>
   <form @submit.prevent="handleSubmit">
      <div class="mb-3">
         <label for="name" class="form-label">{{ $t('message.name') }} *</label>
         <input type="text" class="form-control" id="name" v-model="form.name" required>
      </div>

      <div class="mb-3">
         <label for="country" class="form-label">{{ $t('message.country') }} *</label>
         <input type="text" class="form-control" id="country" v-model="form.country" required>
      </div>

      <div class="mb-3">
         <label for="region" class="form-label">{{ $t('message.region') }}</label>
         <input type="text" class="form-control" id="region" v-model="form.region">
      </div>

      <div class="mb-3">
         <label for="hutWebsite" class="form-label">{{ $t('message.website') }}</label>
         <input type="url" class="form-control" id="hutWebsite" v-model="form.hutWebsite">
      </div>

      <div class="mb-3">
         <label for="link" class="form-label">{{ $t('message.onlineBookingLink') }}</label>
         <input type="url" class="form-control" id="link" v-model="form.link">
      </div>

      <template v-if="form.latitude && form.longitude">
         <div class="row">
            <div class="col">
               <div class="mb-3">
                  <label for="latitude" class="form-label">{{ $t('message.latitude') }}</label>
                  <input type="number" step="0.00000000000001" class="form-control" id="latitude" v-model="form.latitude">
               </div>
            </div>
            <div class="col">
               <div class="mb-3">
                  <label for="longitude" class="form-label">{{ $t('message.longitude') }}</label>
                  <input type="number" step="0.00000000000001" class="form-control" id="longitude" v-model="form.longitude">
               </div>
            </div>
            <div class="col">
               <div class="mb-3">
                  <label for="altitude" class="form-label">{{ $t('message.altitude') }}</label>
                  <input type="number" class="form-control" id="altitude" v-model="form.altitude">
               </div>
            </div>
         </div>
      </template>
      <template v-else>
         <div class="mb-3">
            <button type="button" class="btn btn-secondary" @click="addLocation">
               {{ $t('message.addLocation') }}
            </button>
         </div>
      </template>

      <div class="mb-3">
         <div class="map-container">
            <l-map ref="map" v-model:zoom="zoom" :center="mapCenter" :minZoom="6" :maxZoom="17" style="height: 400px; width: 100%;" @ready="onMapReady">
               <l-control-layers position="topright"></l-control-layers>
               <l-tile-layer v-for="tileProvider in tileProviders" :key="tileProvider.name" :name="tileProvider.name"
                  :visible="tileProvider.visible" :url="tileProvider.url" :attribution="tileProvider.attribution"
                  layer-type="base" />
               <l-marker v-if="form.latitude && form.longitude" :lat-lng="[form.latitude, form.longitude]" draggable
                  @moveend="updateMarkerPosition" :icon="markerIcon">
               </l-marker>
            </l-map>
         </div>
      </div>

      <div class="mb-3">
         <div class="form-check">
            <input type="checkbox" class="form-check-input" id="enabled" v-model="form.enabled">
            <label class="form-check-label" for="enabled">{{ $t('message.enabled') }}</label>
         </div>
      </div>

      <div class="modal-footer">
         <button type="button" class="btn btn-secondary" @click="$emit('cancel')">
            {{ $t('message.cancel') }}
         </button>
         <button type="submit" class="btn btn-primary">
            {{ $t('message.save') }}
         </button>
      </div>
   </form>
</template>

<script>
import { tileProviders } from "../services/mapview-service";
import {
   LMap,
   LTileLayer,
   LMarker,
   LControlLayers,
} from "@vue-leaflet/vue-leaflet";
import "leaflet/dist/leaflet.css";
import L from 'leaflet';

const markerIcon = L.icon({
   iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-blue.png',
   iconSize: [25, 41],
   iconAnchor: [12, 41],
   popupAnchor: [1, -34]
});

export default {
   components: {
      LMap,
      LTileLayer,
      LMarker,
      LControlLayers,
   },
   props: {
      hut: {
         type: Object,
         required: true
      }
   },
   data() {
      const form = this.initForm();
      return {
         form,
         zoom: 10,
         mapCenter: form.latitude && form.longitude ? [form.latitude, form.longitude] : [47.5, 13.5],
         tileProviders,
         markerIcon
      }
   },
   methods: {
      addLocation() {
         const map = this.$refs.map;
         if (map) {
            const center = map.leafletObject.getCenter();
            this.form.latitude = Number(center.lat.toFixed(8));
            this.form.longitude = Number(center.lng.toFixed(8));
         }
      },
      initForm() {
         return {
            id: this.hut.id,
            name: this.hut.name,
            country: this.hut.country,
            region: this.hut.region,
            hutWebsite: this.hut.hutWebsite,
            link: this.hut.link,
            latitude: this.hut.latitude,
            longitude: this.hut.longitude,
            altitude: this.hut.altitude,
            enabled: this.hut.enabled,
            lastUpdated: this.hut.lastUpdated,
            added: this.hut.added,
            activated: this.hut.activated,
            manuallyEdited: this.hut.manuallyEdited
         }
      },
      handleSubmit() {
         this.$emit('save', { ...this.form });
      },
      updateMarkerPosition(event) {
         const latLng = event.target.getLatLng();
         this.form.latitude = Number(latLng.lat.toFixed(8));
         this.form.longitude = Number(latLng.lng.toFixed(8));
      },
      onMapReady(mapObject) {
         setTimeout(() => {
            mapObject.invalidateSize();
            // Re-center map after initialization
            if (this.form.latitude && this.form.longitude) {
               mapObject.setView([this.form.latitude, this.form.longitude], this.zoom);
            }
         }, 500);
      }
   },
   watch: {
      hut: {
         handler(newHut) {
            this.form = this.initForm();
            if (newHut.latitude && newHut.longitude) {
               this.mapCenter = [newHut.latitude, newHut.longitude];
               // Update map view when hut changes
               this.$nextTick(() => {
                  const map = this.$refs.map;
                  if (map) {
                     map.leafletObject.setView([newHut.latitude, newHut.longitude], 12);
                  }
               });
            }
         },
         deep: true
      },
      'form.latitude': function(val) {
         if (val && this.form.longitude) {
            this.mapCenter = [val, this.form.longitude];
         }
      },
      'form.longitude': function(val) {
         if (val && this.form.latitude) {
            this.mapCenter = [this.form.latitude, val];
         }
      }
   },
   mounted() {
      if (this.form.latitude && this.form.longitude) {
         this.mapCenter = [this.form.latitude, this.form.longitude];
         this.zoom = 12;
      }
   }
}
</script>

<style scoped>
.form-label {
   font-weight: 500;
}

.modal-footer {
   padding: 1rem 0 0 0;
   border-top: 1px solid #dee2e6;
}

.map-container {
   border: 1px solid #dee2e6;
   border-radius: 0.25rem;
}
</style>
