<template>
   <section class="container-fluid">
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
                  <a :href="`${hut.link}`" target="_blank" class="action-btn btn-primary">
                     <i class="booking-icon">🔖</i> {{ $t('message.onlineBooking') }}
                  </a>
               </div>
               <div v-else class="booking-inactive">
                  <a :href="`${hut.link}`" target="_blank" class="action-btn btn-secondary">
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
                  <div class="custom-checkbox-group">
                     <div v-for="weekday in weekdays" :key="weekday.key" class="custom-checkbox-option">
                        <input type="checkbox" :id="weekday.key" :value="weekday.key" v-model="selectedWeekdays" class="custom-checkbox">
                        <label :for="weekday.key" class="custom-checkbox-label">{{ $t(weekday.label) }}</label>
                     </div>
                  </div>
               </div>
               
               <!-- Availability card-based layout -->
               <div class="availability-container">
                  <!-- No results message when filtering returns empty -->
                  <div v-if="selectedWeekdays.length > 0 && availabilityByMonth.length === 0" class="no-results-message">
                     <p>{{ $t('message.noResultsFound') }}</p>
                     <button @click="selectedWeekdays = []" class="btn-clear-filter">{{ $t('message.clearFilter') }}</button>
                  </div>
                  
                  <!-- Empty filter result message -->
                  <div v-else-if="availabilityByMonth.length === 1 && availabilityByMonth[0].isEmptyFilterResult" class="no-results-message">
                     <p>{{ $t('message.noResultsFound') }}</p>
                     <button @click="selectedWeekdays = []" class="btn-clear-filter">{{ $t('message.clearFilter') }}</button>
                  </div>
                  
                  <!-- Display available months -->
                  <div v-else v-for="month in this.availabilityByMonth" 
                       :key="month.month" 
                       class="availability-month">
                     <div class="month-header" @click="toggleCollapse(month)">
                        <h4 class="month-title">{{ month.month }}</h4>
                        <span v-if="month.collapsed" class="collapse-icon">▼</span>
                        <span v-else class="collapse-icon">▲</span>
                     </div>
                     
                     <div v-if="!month.collapsed" class="month-content">
                        <div v-for="av in month.availabilities" :key="av.date" class="date-card">
                           <!-- Date header -->
                           <div class="date-header">
                              <div class="date-info">
                                 <span class="date-day">{{ new Date(av.date).getDate() }}</span>
                                 <span class="date-weekday">{{ getWeekdayShort(av.date) }}</span>
                              </div>
                           </div>
                           
                           <!-- Hut closed message -->
                           <div v-if="av.hutClosed" class="closed-message">
                              {{ $t('message.hutClosed') }}
                           </div>
                           
                           <!-- Room availability cards -->
                           <div v-else-if="av.roomAvailabilities && av.roomAvailabilities.length > 0" class="room-availability-container">
                              <div v-for="(roomAv, index) in av.roomAvailabilities" 
                                   :key="`${av.date}-${roomAv.bedCategory}-${index}`" 
                                   :class="['room-availability-card', getAvailabilityClass(roomAv.freeBeds, roomAv.totalBeds)]">
                                 <div class="room-type">{{ roomAv.bedCategory }}</div>
                                 <div class="availability-details">
                                    <div class="bed-info">
                                       <span class="bed-count">{{ roomAv.freeBeds }}</span>
                                       <span class="bed-separator">/</span>
                                       <span class="bed-total">{{ roomAv.totalBeds }}</span>
                                       <span class="beds-label"> {{ $t('message.beds') }}</span>
                                    </div>
                                    <div class="availability-indicator">
                                       <div class="indicator-bar" :style="getAvailabilityBarStyle(roomAv.freeBeds, roomAv.totalBeds)"></div>
                                    </div>
                                 </div>
                              </div>
                           </div>
                           
                           <!-- No availability info -->
                           <div v-else class="no-availability-info">
                              {{ $t('message.noAvailabilityInfo') }}
                           </div>
                        </div>
                     </div>
                  </div>
               </div>
            </div>
         </div>
      </div>
   </section>
</template>

<style scoped>
/* Component-specific styles */
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
   background: url('https://images.unsplash.com/photo-1588668214407-6ea9a6d8c272?ixlib=rb-4.0.3&auto=format&fit=crop&w=1050&q=80') center/cover;
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

.booking-icon {
   font-size: 1.2rem;
}

/* Info items */
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

/* Map container */
.map-container {
   height: 350px;
   width: 100%;
}

/* Availability section styling */
.availability-section {
   margin-top: 2rem;
}

.section-title {
   font-size: 1.5rem;
   margin-bottom: 1.5rem;
   color: #333;
}

.filter-card {
   background-color: #f8f9fa;
   border-radius: 8px;
   padding: 1.5rem;
   margin-bottom: 1.5rem;
   box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.filter-title {
   margin-bottom: 1rem;
   font-size: 1.1rem;
   color: #333;
}

.custom-checkbox-group {
   display: flex;
   flex-wrap: wrap;
   gap: 10px;
}

.custom-checkbox-option {
   display: flex;
   align-items: center;
   margin-right: 12px;
}

.custom-checkbox {
   margin-right: 6px;
}

/* No results message */
.no-results-message {
   background-color: #f8f9fa;
   border-radius: 8px;
   padding: 2rem;
   text-align: center;
   box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
   min-height: 200px;
   display: flex;
   flex-direction: column;
   align-items: center;
   justify-content: center;
   gap: 1rem;
}

.no-results-message p {
   color: #666;
   font-size: 1.1rem;
   margin-bottom: 1rem;
}

.btn-clear-filter {
   background-color: #3498db;
   color: white;
   border: none;
   padding: 8px 16px;
   border-radius: 4px;
   cursor: pointer;
   font-weight: 500;
   transition: background-color 0.2s;
}

.btn-clear-filter:hover {
   background-color: #2980b9;
}

/* New Card-based Availability Layout */
.availability-container {
   display: flex;
   flex-direction: column;
   gap: 1.5rem;
}

.availability-month {
   border-radius: 8px;
   overflow: hidden;
   box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
   background-color: white;
}

.month-header {
   background-color: #f5f7fa;
   padding: 14px 20px;
   font-weight: 600;
   color: #333;
   cursor: pointer;
   position: relative;
   display: flex;
   justify-content: space-between;
   align-items: center;
   transition: background-color 0.2s;
}

.month-header:hover {
   background-color: #edf0f5;
}

.month-title {
   margin: 0;
   font-size: 1.1rem;
}

.collapse-icon {
   font-size: 0.8rem;
   color: #7f8c8d;
}

.month-content {
   padding: 10px;
   display: grid;
   grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
   gap: 15px;
}

.date-card {
   border: 1px solid #eaeaea;
   border-radius: 8px;
   overflow: hidden;
   background-color: white;
   box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
}

.date-header {
   background-color: #f8f9fa;
   padding: 12px 15px;
   border-bottom: 1px solid #eaeaea;
}

.date-info {
   display: flex;
   align-items: baseline;
   gap: 8px;
}

.date-day {
   font-size: 1.3rem;
   font-weight: 600;
   color: #333;
}

.date-weekday {
   font-size: 0.9rem;
   color: #666;
   font-weight: 500;
}

.room-availability-container {
   padding: 12px;
   display: flex;
   flex-direction: column;
   gap: 10px;
}

.room-availability-card {
   padding: 12px;
   border-radius: 6px;
   background-color: #f5f7fa;
   display: flex;
   flex-direction: column;
   gap: 8px;
}

.room-availability-card.availability-high {
   background-color: rgba(46, 204, 113, 0.1);
   border-left: 3px solid #2ecc71;
}

.room-availability-card.availability-medium {
   background-color: rgba(243, 156, 18, 0.1);
   border-left: 3px solid #f39c12;
}

.room-availability-card.availability-low {
   background-color: rgba(231, 76, 60, 0.1);
   border-left: 3px solid #e74c3c;
}

.room-type {
   font-weight: 600;
   color: #333;
}

.availability-details {
   display: flex;
   flex-direction: column;
   gap: 6px;
}

.bed-info {
   display: flex;
   align-items: center;
   gap: 3px;
}

.bed-count {
   font-weight: 600;
   font-size: 1.1rem;
}

.bed-separator {
   color: #999;
}

.bed-total {
   color: #666;
}

.beds-label {
   color: #777;
   font-size: 0.9rem;
}

.availability-indicator {
   height: 4px;
   width: 100%;
   background-color: #f0f0f0;
   border-radius: 2px;
   overflow: hidden;
}

.indicator-bar {
   height: 100%;
   border-radius: 2px;
}

.closed-message {
   padding: 15px;
   font-style: italic;
   color: #e74c3c;
   text-align: center;
}

.no-availability-info {
   padding: 15px;
   color: #7f8c8d;
   text-align: center;
   font-style: italic;
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
   
   .month-content {
      grid-template-columns: 1fr;
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
         weekdays: [
            { key: 'sunday', label: 'message.sunday' },
            { key: 'monday', label: 'message.monday' },
            { key: 'tuesday', label: 'message.tuesday' },
            { key: 'wednesday', label: 'message.wednesday' },
            { key: 'thursday', label: 'message.thursday' },
            { key: 'friday', label: 'message.friday' },
            { key: 'saturday', label: 'message.saturday' }
         ],
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
         
         // Create a mapping of our weekday keys to the day of the week (0-6)
         const weekdayMap = {
            'sunday': 0,
            'monday': 1, 
            'tuesday': 2, 
            'wednesday': 3, 
            'thursday': 4, 
            'friday': 5, 
            'saturday': 6
         };
         
         // Convert our selected weekday keys into day numbers
         const selectedDayNumbers = this.selectedWeekdays.map(day => weekdayMap[day]);
         
         // Filter availabilities where the day of the week matches one of our selected days
         return availabilities.filter(av => {
            const date = new Date(av.date);
            const dayOfWeek = date.getDay(); // 0 = Sunday, 1 = Monday, etc.
            return selectedDayNumbers.includes(dayOfWeek);
         });
      },
      groupByMonth(availabilities) {
         const filteredAvailabilities = this.filterByWeekdays(availabilities);
         const months = {};
         
         // Store current collapse states
         const currentCollapseStates = {};
         if (this.availabilityByMonth.length > 0) {
            this.availabilityByMonth.forEach(month => {
               currentCollapseStates[month.month] = month.collapsed;
            });
         }
         
         filteredAvailabilities.forEach(av => {
            const month = this.getMonthYearDisplay(av.date);
            if (!months[month]) {
               // Use previous collapse state if available, otherwise use default
               const wasCollapsed = currentCollapseStates[month] !== undefined ? 
                                   currentCollapseStates[month] : 
                                   !(new Date(av.date).getMonth() === new Date().getMonth() && 
                                     new Date(av.date).getFullYear() === new Date().getFullYear());
               
               months[month] = { 
                  month, 
                  availabilities: [], 
                  collapsed: wasCollapsed
               };
            }
            months[month].availabilities.push(av);
         });
         
         // If no months after filtering, preserve the empty state but with filtered message
         if (Object.keys(months).length === 0 && this.selectedWeekdays.length > 0) {
            return [{
               month: this.$t('message.noResultsFound'), 
               availabilities: [],
               collapsed: false,
               isEmptyFilterResult: true
            }];
         }
         
         return Object.values(months);
      },
      getAvailabilityClass(freeBeds, totalBeds) {
         // No beds available
         if (freeBeds === 0) {
            return 'availability-low';
         }
         
         // At least 4 beds or 10% of total beds are available
         if (freeBeds >= 4 || (freeBeds / totalBeds) >= 0.1) {
            return 'availability-high';
         }
         
         // Otherwise (between 1-3 beds and less than 10%)
         return 'availability-medium';
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
      getMonthYearDisplay(date) {
         const d = new Date(date);
         const month = d.getMonth(); // 0-11
         const year = d.getFullYear();
         
         // Get the translated month name
         const monthKey = [
            'message.january', 'message.february', 'message.march', 'message.april',
            'message.may', 'message.june', 'message.july', 'message.august',
            'message.september', 'message.october', 'message.november', 'message.december'
         ][month];
         
         return `${this.$t(monthKey)} ${year}`;
      },
      
      getWeekdayShort(date) {
         const d = new Date(date);
         const day = d.getDay(); // 0-6, 0 is Sunday
         
         // Get the translated short weekday name
         const dayKey = [
            'message.sun', 'message.mon', 'message.tue', 'message.wed',
            'message.thu', 'message.fri', 'message.sat'
         ][day];
         
         return this.$t(dayKey);
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
