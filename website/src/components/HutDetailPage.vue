<template>
   <section class="hut-detail-container">
      <div v-show="loading" class="loading-container">
         <div class="spinner"></div>
         <p>{{ $t('message.loading') }}...</p>
      </div>

      <div v-if="!loading && hut != null" class="hut-detail">
         <!-- Hero section with image backdrop and hut name -->
         <div class="hero-section">
            <div class="hero-content">
               <h1 class="hut-name">{{ hut.name }}</h1>
               <div class="hut-location">
                  <i class="location-icon">📍</i>
                  <span>{{ hut.country }}</span>
                  <span v-if="hut.region != null"> - {{ hut.region }}</span>
                  <span v-if="hut.altitude != null" class="altitude-badge">{{ hut.altitude }}m</span>
               </div>
               <div class="booking-buttons" v-if="hut.enabled">
                  <a :href="`${hut.link}`" target="_blank" class="btn btn-primary booking-btn">
                     <i class="booking-icon">🔖</i> {{ $t('message.onlineBooking') }}
                  </a>
               </div>
               <div v-else class="booking-inactive">
                  <a :href="`${hut.link}`" target="_blank" class="btn btn-secondary">
                     <i class="booking-icon">🔖</i> {{ $t('message.onlineBookingInactive') }}
                  </a>
               </div>
            </div>
         </div>

         <div class="content-section">
            <div class="row">
               <!-- Info card -->
               <div class="col-md-5">
                  <div class="info-card">
                     <h3 class="card-title">{{ $t('message.hut') }} {{ $t('message.info') }}</h3>
                     
                     <div class="info-item">
                        <div class="info-label">{{ $t('message.website') }}</div>
                        <div class="info-value">
                           <a :href="`${hut.hutWebsite}`" target="_blank" class="website-link">
                              {{ shortWebsiteUrl(hut.hutWebsite) }}
                              <i class="external-link-icon">↗️</i>
                           </a>
                        </div>
                     </div>

                     <div class="info-item">
                        <div class="info-label">{{ $t('message.coordinates') }}</div>
                        <div class="info-value">
                           <router-link v-if="hut.latitude != null && hut.longitude != null"
                              :to="{ name: 'mapPage', query: { hutId: hut.id } }" :title="$t('message.showOnMap')" class="coordinates-link">
                              {{ hut.latitude }}/{{ hut.longitude }}
                              <i class="map-icon">🗺️</i>
                           </router-link>
                        </div>
                     </div>

                     <div class="info-item">
                        <div class="info-label">{{ $t('message.lastUpdated') }}</div>
                        <div class="info-value">{{ new Date(hut.lastUpdated).toLocaleString() }}</div>
                     </div>

                     <div class="info-item">
                        <div class="info-label">{{ $t('message.hutAdded') }}</div>
                        <div class="info-value">{{ new Date(hut.added).toLocaleDateString() }}</div>
                     </div>
                  </div>
               </div>

               <!-- Map card -->
               <div class="col-md-7">
                  <div class="map-card">
                     <div class="map-container">
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
            </div>

            <!-- Availability section -->
            <div class="availability-section" v-if="hut.enabled">
               <h3 class="section-title">{{ $t('message.beds') }} {{ $t('message.typeOfAccommodation') }}</h3>
               
               <!-- Weekday filter -->
               <div class="filter-card">
                  <h4 class="filter-title">{{ $t('message.filterByWeekdays') }}</h4>
                  <div class="weekday-filter">
                     <div v-for="weekday in weekdays" :key="weekday" class="weekday-option">
                        <input type="checkbox" :id="weekday" :value="weekday" v-model="selectedWeekdays" class="weekday-checkbox">
                        <label :for="weekday" class="weekday-label">{{ weekday }}</label>
                     </div>
                  </div>
               </div>
               
               <!-- Availability table -->
               <div class="availability-table-container">
                  <table class="availability-table">
                     <thead>
                        <tr>
                           <th>{{ $t('message.date') }}</th>
                           <th>{{ $t('message.beds') }}</th>
                           <th>{{ $t('message.typeOfAccommodation') }}</th>
                        </tr>
                     </thead>
                     <tbody>
                        <template v-for="month in this.availabilityByMonth" :key="month">
                           <tr @click="toggleCollapse(month)" class="month-row">
                              <th colspan="3" class="month-header">
                                 {{ month.month }}
                                 <span v-if="month.collapsed" class="collapse-icon">▼</span>
                                 <span v-else class="collapse-icon">▲</span>
                              </th>
                           </tr>
                           <template v-if="!month.collapsed" v-for="av in month.availabilities">
                              <tr v-for="(roomAv, iSub) in av.roomAvailabilities" :key="roomAv.bedCategory" class="availability-row">
                                 <td v-if="iSub === 0" :rowspan="av.roomAvailabilities.length" class="date-cell">
                                    <span class="date-day">{{ new Date(av.date).getDate() }}</span>
                                    <span class="date-weekday">{{ new Date(av.date).toLocaleString('default', { weekday: 'short' }) }}</span>
                                 </td>
                                 <td :class="['bed-cell', getBedAvailabilityClass(roomAv.freeBeds, roomAv.totalBeds)]">
                                    <div class="bed-info">
                                       <span class="bed-count">{{ roomAv.freeBeds }}</span>
                                       <span class="bed-separator">/</span>
                                       <span class="bed-total">{{ roomAv.totalBeds }}</span>
                                    </div>
                                    <div class="availability-indicator">
                                       <div class="indicator-bar" :style="getAvailabilityBarStyle(roomAv.freeBeds, roomAv.totalBeds)"></div>
                                    </div>
                                 </td>
                                 <td :class="['type-cell', getBedAvailabilityClass(roomAv.freeBeds, roomAv.totalBeds)]">
                                    {{ roomAv.bedCategory }}
                                 </td>
                              </tr>
                              <tr v-if="av.hutClosed" class="hut-closed-row">
                                 <td class="date-cell">
                                    <span class="date-day">{{ new Date(av.date).getDate() }}</span>
                                    <span class="date-weekday">{{ new Date(av.date).toLocaleString('default', { weekday: 'short' }) }}</span>
                                 </td>
                                 <td colspan="2" class="closed-message">{{ $t('message.hutClosed') }}</td>
                              </tr>
                           </template>
                        </template>
                     </tbody>
                  </table>
               </div>
            </div>
         </div>
      </div>
   </section>
</template>

<style scoped>
/* General Layout */
.hut-detail-container {
   max-width: 1200px;
   margin: 0 auto;
   padding: 0 20px;
   font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
}

.hut-detail {
   display: flex;
   flex-direction: column;
   gap: 2rem;
}

.content-section {
   display: flex;
   flex-direction: column;
   gap: 2rem;
}

/* Loading Animation */
.loading-container {
   display: flex;
   flex-direction: column;
   align-items: center;
   justify-content: center;
   height: 200px;
}

.spinner {
   width: 40px;
   height: 40px;
   border: 4px solid rgba(0, 0, 0, 0.1);
   border-radius: 50%;
   border-top: 4px solid #3498db;
   animation: spin 1s linear infinite;
   margin-bottom: 1rem;
}

@keyframes spin {
   0% { transform: rotate(0deg); }
   100% { transform: rotate(360deg); }
}

/* Hero Section */
.hero-section {
   background: linear-gradient(to right, #3494e6, #ec6ead);
   border-radius: 12px;
   padding: 3rem 2rem;
   color: white;
   box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
   position: relative;
   overflow: hidden;
}

.hero-section::before {
   content: "";
   position: absolute;
   top: 0;
   left: 0;
   right: 0;
   bottom: 0;
   background: url('https://images.unsplash.com/photo-1520681493044-2405cb9ce2bf?ixlib=rb-4.0.3&auto=format&fit=crop&w=1050&q=80') center/cover;
   opacity: 0.3;
   z-index: 0;
}

.hero-content {
   position: relative;
   z-index: 1;
}

.hut-name {
   font-size: 2.5rem;
   font-weight: 700;
   margin-bottom: 0.5rem;
   text-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
}

.hut-location {
   display: flex;
   align-items: center;
   gap: 8px;
   font-size: 1.1rem;
   margin-bottom: 1.5rem;
}

.location-icon {
   font-size: 1.2rem;
}

.altitude-badge {
   background-color: rgba(255, 255, 255, 0.2);
   border-radius: 20px;
   padding: 2px 12px;
   margin-left: 8px;
   font-size: 0.9rem;
}

.booking-buttons {
   margin-top: 1rem;
}

.booking-btn {
   display: inline-flex;
   align-items: center;
   gap: 8px;
   padding: 10px 24px;
   border-radius: 8px;
   background-color: #2ecc71;
   color: white;
   text-decoration: none;
   font-weight: 600;
   transition: all 0.2s ease;
   border: none;
   cursor: pointer;
}

.booking-btn:hover {
   background-color: #27ae60;
   transform: translateY(-2px);
   box-shadow: 0 4px 8px rgba(0, 0, 0, 0.15);
}

.booking-inactive .btn {
   background-color: #95a5a6;
   color: white;
   display: inline-flex;
   align-items: center;
   gap: 8px;
   padding: 10px 24px;
   border-radius: 8px;
   text-decoration: none;
   font-weight: 600;
   transition: all 0.2s ease;
   border: none;
}

.booking-inactive .btn:hover {
   background-color: #7f8c8d;
}

.booking-icon {
   font-size: 1.2rem;
}

/* Info Card */
.info-card {
   background-color: white;
   border-radius: 12px;
   padding: 1.5rem;
   box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
   height: 100%;
}

.card-title {
   font-size: 1.4rem;
   margin-bottom: 1.5rem;
   color: #333;
   font-weight: 600;
   border-bottom: 2px solid #f5f5f5;
   padding-bottom: 10px;
}

.info-item {
   display: flex;
   margin-bottom: 1rem;
   padding-bottom: 0.5rem;
   border-bottom: 1px solid #f5f5f5;
}

.info-label {
   flex: 0 0 40%;
   font-weight: 500;
   color: #666;
}

.info-value {
   flex: 0 0 60%;
   color: #333;
}

.website-link, .coordinates-link {
   color: #3498db;
   text-decoration: none;
   display: inline-flex;
   align-items: center;
   gap: 5px;
   transition: color 0.2s;
}

.website-link:hover, .coordinates-link:hover {
   color: #2980b9;
   text-decoration: underline;
}

.external-link-icon, .map-icon {
   font-size: 0.9rem;
}

/* Map Card */
.map-card {
   background-color: white;
   border-radius: 12px;
   overflow: hidden;
   box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
   height: 100%;
}

.map-container {
   height: 350px;
   width: 100%;
}

/* Availability Section */
.availability-section {
   margin-top: 1rem;
}

.section-title {
   font-size: 1.6rem;
   margin-bottom: 1.5rem;
   color: #333;
   font-weight: 600;
}

/* Filter Card */
.filter-card {
   background-color: white;
   border-radius: 12px;
   padding: 1.5rem;
   margin-bottom: 1.5rem;
   box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
}

.filter-title {
   font-size: 1.2rem;
   margin-bottom: 1rem;
   color: #333;
   font-weight: 600;
}

.weekday-filter {
   display: flex;
   flex-wrap: wrap;
   gap: 12px;
}

.weekday-option {
   position: relative;
}

.weekday-checkbox {
   position: absolute;
   opacity: 0;
   cursor: pointer;
   height: 0;
   width: 0;
}

.weekday-label {
   display: inline-block;
   padding: 8px 16px;
   border-radius: 30px;
   background-color: #f5f5f5;
   color: #666;
   cursor: pointer;
   transition: all 0.2s;
   user-select: none;
}

.weekday-checkbox:checked + .weekday-label {
   background-color: #3498db;
   color: white;
   box-shadow: 0 2px 5px rgba(52, 152, 219, 0.3);
}

.weekday-checkbox:hover + .weekday-label {
   background-color: #e0e0e0;
}

.weekday-checkbox:checked:hover + .weekday-label {
   background-color: #2980b9;
}

/* Availability Table */
.availability-table-container {
   background-color: white;
   border-radius: 12px;
   overflow: hidden;
   box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
}

.availability-table {
   width: 100%;
   border-collapse: collapse;
}

.availability-table th, .availability-table td {
   padding: 12px 16px;
   border-bottom: 1px solid #f0f0f0;
}

.availability-table thead th {
   background-color: #f8f9fa;
   font-weight: 600;
   color: #555;
   text-align: left;
}

.month-row {
   cursor: pointer;
}

.month-header {
   background-color: #f0f4f8;
   color: #2c3e50;
   text-align: left;
   font-weight: 600;
   position: relative;
   transition: background-color 0.2s;
}

.month-header:hover {
   background-color: #e4ebf5;
}

.collapse-icon {
   position: absolute;
   right: 16px;
   top: 50%;
   transform: translateY(-50%);
   font-size: 0.8rem;
   color: #7f8c8d;
}

.date-cell {
   display: flex;
   flex-direction: column;
   white-space: nowrap;
}

.date-day {
   font-size: 1.2rem;
   font-weight: 600;
}

.date-weekday {
   font-size: 0.8rem;
   color: #666;
}

.bed-cell, .type-cell {
   transition: background-color 0.2s;
}

.bed-info {
   display: flex;
   align-items: center;
   gap: 3px;
}

.bed-count {
   font-weight: 600;
   font-size: 1rem;
}

.bed-separator {
   color: #999;
}

.bed-total {
   color: #666;
}

.availability-indicator {
   height: 4px;
   width: 100%;
   background-color: #f0f0f0;
   border-radius: 2px;
   margin-top: 5px;
   overflow: hidden;
}

.indicator-bar {
   height: 100%;
   border-radius: 2px;
}

.hut-closed-row {
   background-color: #f8f9fa;
}

.closed-message {
   font-style: italic;
   color: #e74c3c;
   text-align: center;
}

/* Availability colors - enhanced */
.bed-availability-high {
   background-color: rgba(46, 204, 113, 0.1);
   color: #27ae60;
}

.bed-availability-medium {
   background-color: rgba(255, 159, 64, 0.1);
   color: #f39c12;
}

.bed-availability-low {
   background-color: rgba(231, 76, 60, 0.1);
   color: #c0392b;
}

/* Responsive adjustments */
@media (max-width: 768px) {
   .hero-section {
      padding: 2rem 1.5rem;
   }
   
   .hut-name {
      font-size: 2rem;
   }
   
   .info-item {
      flex-direction: column;
   }
   
   .info-label, .info-value {
      flex: 0 0 100%;
   }
   
   .info-label {
      margin-bottom: 5px;
   }
   
   .weekday-filter {
      justify-content: center;
   }
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
      getAvailabilityBarStyle(freeBeds, totalBeds) {
         if (totalBeds === 0) return { width: '0%', backgroundColor: '#e74c3c' };
         
         const percentage = Math.min(100, Math.round((freeBeds / totalBeds) * 100));
         
         let color = '#e74c3c'; // Default red
         if (freeBeds === 0) {
            color = '#e74c3c'; // Red
         } else if (freeBeds >= 4 || (freeBeds / totalBeds) >= 0.1) {
            color = '#2ecc71'; // Green
         } else {
            color = '#f39c12'; // Orange
         }
         
         return {
            width: `${percentage}%`,
            backgroundColor: color
         };
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
