<template>
  <div>
    <button class="toggle-btn" :class="{ 'collapsed': isCollapsed }" @click="isCollapsed = !isCollapsed">
      {{ isCollapsed ? '☰' : '✕' }}
    </button>
    <div class="commandbar" :class="{ 'collapsed': isCollapsed }">
      <div class="controls-content">
        <div class="control-group">
          <label>{{ $t('mapPage.availabilityAtDate') }}</label>
          <div class="input-group">
            <input v-model="dateFilter" type="date" :min="`${new Date().toISOString().split('T')[0]}`" class="form-control" />
          </div>
        </div>
        <div class="control-group">
          <label>{{ $t('mapPage.numberOfBeds') }}</label>
          <div class="input-group number-input">
            <button class="number-btn" @click="decrementBeds" :disabled="desiredNumberOfBeds <= 1">−</button>
            <input v-model="desiredNumberOfBeds" type="number" min="1" max="10" inputmode="numeric" class="form-control" />
            <button class="number-btn" @click="incrementBeds" :disabled="desiredNumberOfBeds >= 10">+</button>
          </div>
        </div>
        <div class="control-group">
          <label>{{ $t('mapPage.bedCategory') }}</label>
          <div class="input-group">
            <select v-model="selectedBedCategory" class="form-control">
              <option value="">-{{ $t('mapPage.anyBeds') }}-</option>
              <option v-for="option in this.bedCategories" :value="option.name">
                {{ option.name }}
              </option>
            </select>
          </div>
        </div>
        <div class="control-group">
          <label>{{ $t('message.search') }}</label>
          <div class="input-group">
            <vue3-simple-typeahead :items="huts" :minInputLength="1"
              :itemProjection="(hut) => { return hut.name; }" @selectItem="hutSelected">
            </vue3-simple-typeahead>
          </div>
        </div>

      </div>
    </div>

    <div id="mainmap" style="z-index: 1;">
      <loading v-model:active="loading" />
      <l-map ref="map" v-model:zoom="zoom" :center="mapCenter" :minZoom="6" :maxZoom="17" :options="{ zoomControl: false }">
        <l-control-layers position="topright"></l-control-layers>
        <l-tile-layer v-for="tileProvider in tileProviders" :key="tileProvider.name" :name="tileProvider.name"
          :visible="tileProvider.visible" :url="tileProvider.url" :attribution="tileProvider.attribution"
          layer-type="base" />
        <template v-for="hut in this.huts">
          <l-marker ref="markerItems" :name="hut.name" :lat-lng="[hut.latitude, hut.longitude]" :icon="getHutMarkerIcon(hut)">
            <l-tooltip>
              <b>{{ hut.name }}</b>
              <div v-if="hut.availability != null && !hut.availability.hutClosed">{{ $t('mapPage.freeBeds') }}: {{
                hut.availability.totalFreeBeds }} / {{
                hut.availability.totalBeds }}</div>
              <div v-if="hut.availability?.hutClosed"><i>{{ $t('message.hutClosed') }}</i></div> <!-- Blue marker -->
              <div v-if="!hut.enabled"><i>{{ $t('message.hutNotYetActive') }}</i></div> <!-- Grey marker -->
            </l-tooltip>
            <l-popup :options='{ "closeButton": false }' @remove="onPopupClose">
              <h6>
                <router-link :to="{ name: 'hutDetailsPage', params: { hutId: hut.id } }"
                  title="{{ $t('message.showHutDetails') }}">{{
                  hut.name
                  }}</router-link>
              </h6>
              <div>
                <span v-if="hut.enabled">[{{ new Date(this.dateFilter).toLocaleDateString()
                }}] </span>
                <span v-if="hut.availability != null && !hut.availability.hutClosed">{{ $t('mapPage.freeBeds') }}: {{
                  hut.availability.totalFreeBeds }} /
                  {{ hut.availability.totalBeds }}</span>
                <span v-if="hut.availability?.hutClosed">{{ $t('message.hutClosed') }}</span>
                <span v-if="hut.availability == null && hut.enabled">{{ $t('mapPage.noAvailabilityInfo') }}</span>
                <br />
                <a v-if="hut.enabled" :href="`${hut.link}`" target="_blank">{{ $t('message.onlineBooking') }}</a>
                <span v-else><i>{{ $t('message.onlineBookingInactive') }}</i></span>
                <br />
                <a :href="`${hut.hutWebsite}`" target="_blank">{{ shortWebsiteUrl(hut.hutWebsite) }}</a>
                <br />
                <br />
                <table v-if="hut.availability != null">
                  <template v-for="availability in hut.availability?.roomAvailabilities">
                    <tr>
                      <td>{{ availability.bedCategory }}</td>
                      <td>{{ availability.freeBeds }} / {{ availability.totalBeds }}
                      </td>
                    </tr>
                  </template>
                </table>
                <br />
                <template v-if="!hut.availability?.hutClosed && hut.availability?.totalFreeBeds == 0">
                  <div>
                    <div v-if="!formSubmitted">
                      <label>{{ $t('message.hutfullNotify') }}</label>
                      <br />
                      <input type="email" id="email" v-model="email" placeholder="Email" required />
                      <button @click="submitNotificationForm(hut.id)">{{ $t('message.submit') }}</button>
                    </div>
                    <div v-else>
                      <p>{{ $t('message.formSuccessfullySubmitted') }}</p>
                    </div>
                  </div>
                </template>
                <br />
                <span>{{ $t('message.lastUpdated') }}: {{ new
                  Date(hut.availability?.lastUpdated ?? hut.lastUpdated).toLocaleString()
                }} (<a href="#" @click="hutSelected(hut)">Zoom in</a>)</span>
              </div>
            </l-popup>
          </l-marker>
        </template>
      </l-map>
    </div>
  </div>
</template>

<script>
import { Constants } from '../utils';
import { EventBus } from "../main"
import { shortWebsiteUrl } from "../utils"
import { tileProviders } from "../services/mapview-service";

import L from 'leaflet';
import {
  LMap,
  LIcon,
  LTileLayer,
  LMarker,
  LControlLayers,
  LTooltip,
  LPopup,
} from "@vue-leaflet/vue-leaflet";
import "leaflet/dist/leaflet.css";

import Loading from 'vue-loading-overlay';
import 'vue-loading-overlay/dist/css/index.css';

export default {
  props: {
    isAuthenticated: Boolean
  },
  components: {
    LMap,
    LIcon,
    LTileLayer,
    LMarker,
    LControlLayers,
    LTooltip,
    LPopup,
    Loading
  },
  data() {
    return {
      isCollapsed: (() => {
        const savedState = localStorage.getItem('mapMenuCollapsed');
        return savedState !== null ? savedState === 'true' : false;
      })(),
      email: '',
      formSubmitted: false,
      loading: true,
      dateFilter: (() => {
        const queryDate = this.$route.query.date;
        const today = new Date().toISOString().split('T')[0];
        if (queryDate && !isNaN(Date.parse(queryDate)) && queryDate >= today) {
          return queryDate;
        }
        return today;
      })(),
      selectedBedCategory: (() => {
        const queryBedCategory = this.$route.query.bedCategory;
        if (queryBedCategory) {
          return queryBedCategory;
        }
        return "";
      })(),
      desiredNumberOfBeds: (() => {
        const queryNumBeds = this.$route.query.numBeds;
        if (queryNumBeds && !isNaN(parseInt(queryNumBeds)) && parseInt(queryNumBeds) > 0 && parseInt(queryNumBeds) <= 10) {
          return parseInt(queryNumBeds);
        }
        return 1;
      })(),
      mapCenter: [48.00, 11.33],
      zoom: (() => {
        const queryZoom = parseInt(this.$route.query.zoom);
        return (!isNaN(queryZoom) && queryZoom >= 6 && queryZoom <= 17) ? queryZoom : 7;
      })(),
      tileProviders,
      huts: [],
      availabilityData: [],
      bedCategories: [],
      latestQueryString: this.$route.query,
    };
  },
  methods: {
    updateQueryParams(paramName, paramValue) {
      const queryParams = new URLSearchParams(this.latestQueryString);
      if (paramValue === null || paramValue === '' || paramValue === undefined) {
        queryParams.delete(paramName);
      } else {
        queryParams.set(paramName, paramValue);
      }

      const hashWithoutQuery = window.location.hash.split('?')[0];
      const currentPath = window.location.pathname;
      const queryString = queryParams.toString();

      const newUrl = `${currentPath}${hashWithoutQuery}${queryString ? '?' + queryString : ''}`;
      window.history.replaceState({}, document.title, newUrl);

      this.latestQueryString = queryParams;
    },
    shortWebsiteUrl(url) {
      return shortWebsiteUrl(url);
    },
    async updateAvailabilityData() {
      try {
        this.availabilityData = await this.$MapviewService.getAllAvailabilityOnDate(this.dateFilter);
        if (this.huts != null) {
          this.huts.forEach(hut => {
            hut.availability = this.availabilityData.find(a => a.hutId == hut.id) || null;
          });
        }
      }
      catch (e) {
        EventBus.$emit(Constants.EVENT_ERROR, "There was a problem fetching map data. " + e.message);
      }
    },
    getHutMarkerIcon(hut) {
      const redIcon = 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-red.png'
      const greenIcon = 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-green.png'
      const greyIcon = 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-grey.png'
      const blueIcon = 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-blue.png'

      var icon = "";
      if (!hut.enabled) {
        icon = greyIcon
      }
      else {
        var freeBeds = hut.availability?.totalFreeBeds;

        if (freeBeds != null && this.selectedBedCategory != "" && hut.availability.roomAvailabilities != null) {
          var bedCategory = this.selectedBedCategory;
          var filteredAvailability = hut.availability.roomAvailabilities.filter(function (availability) {
            return availability.bedCategory == bedCategory;
          });
          if (filteredAvailability.length > 0) {
            freeBeds = filteredAvailability[0].freeBeds;
          }
          else {
            freeBeds = 0;
          }
        }
        if (freeBeds == null || hut.availability?.hutClosed) {
          icon = blueIcon
        }
        else if (freeBeds >= this.desiredNumberOfBeds) {
          icon = greenIcon
        }
        else {
          icon = redIcon
        }
      }

      return L.icon({
        iconUrl: icon,
        iconSize: [25, 41],
        iconAnchor: [12, 41],
        popupAnchor: [1, -34]
      });
    },
    async hutSelected(hut) {
      this.zoom = 15;
      await this.sleep(50);
      this.mapCenter = [hut.latitude, hut.longitude];
      var marker = this.$refs.markerItems.find(m => m.name == hut.name);
      if (marker != null) {
        marker.leafletObject.openPopup();
      }
    },
    sleep(ms) {
      return new Promise(resolve => setTimeout(resolve, ms));
    },
    async submitNotificationForm(hutId) {
      try {
        await fetch(`/api/freebednotifications/${hutId}`, {
          method: "POST",
          headers: {
            "Content-Type": "application/json"
          },
          body: JSON.stringify({
            emailAddress: this.email,
            date: this.dateFilter
          })
        });
        this.formSubmitted = true;
      } catch (error) {
        console.error("Error submitting form:", error);
      }
    },
    onPopupClose() {
      this.formSubmitted = false;
    },
    incrementBeds() {
      if (this.desiredNumberOfBeds < 10) {
        this.desiredNumberOfBeds++;
      }
    },
    decrementBeds() {
      if (this.desiredNumberOfBeds > 1) {
        this.desiredNumberOfBeds--;
      }
    }
  },
  watch: {
    dateFilter: async function (newValue, oldValue) {
      if(newValue == null || newValue == "") {
        this.dateFilter = new Date().toISOString().split('T')[0];
      }
      this.loading = true;
      await this.updateAvailabilityData();
      this.loading = false;
      this.updateQueryParams('date', newValue);
    },
    desiredNumberOfBeds: function (newValue, oldValue) {
      if (newValue < 1 || newValue > 10) {
        this.desiredNumberOfBeds = 1;
      }
      this.updateQueryParams('numBeds', newValue);
    },
    selectedBedCategory: function (newValue, oldValue) {
      this.updateQueryParams('bedCategory', newValue);
    },
    isCollapsed: function(newValue) {
      localStorage.setItem('mapMenuCollapsed', newValue);
    }
  },
  async mounted() {
    this.bedCategories = await this.$BedCategoryService.getAllBedCategories();
    await this.updateAvailabilityData();
    var allHuts = await this.$HutService.listHutsAsync();
    var hutsWithLocation = allHuts.filter(hut => hut.latitude != null && hut.longitude != null);

    hutsWithLocation.forEach(hut => {
      hut.availability = this.availabilityData.find(a => a.hutId == hut.id) || null;
    });
    this.huts = hutsWithLocation;

    var hutIdQuery = this.$route.query.hutId;
    if (hutIdQuery != null) {
      var hutId = parseInt(hutIdQuery);
      var hut = this.huts.find(hut => hut.id == hutId);
      if (hut != null) {
        await this.hutSelected(hut);
      }
    }

    this.loading = false;
  }
};
</script>

<style>
#mainmap {
  position: fixed;
  top: 70px;  /* Added more space between header and map */
  left: 0;
  right: 0;
  bottom: 0;
  z-index: 1;
}

/* Hide nav-links styles since they're not used anymore */

.commandbar {
  position: fixed;
  top: 80px;  /* Adjusted to match new map spacing */
  left: 72px;
  background: rgba(255, 255, 255, 0.98);
  border-radius: 8px;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.2);
  z-index: 1500;
  transition: transform 0.3s ease;
  overflow: visible !important;
  max-width: 350px;
  width: calc(100% - 92px);
  height: auto;
  max-height: calc(100vh - 80px);
}

.controls-content {
  position: relative;
  padding: 15px;
  height: auto;
}

.control-group {
  position: relative !important;
  margin-bottom: 15px !important;
}

.control-group:last-child {
  margin-bottom: 0 !important;
}

.input-group {
  position: relative !important;
  width: 100% !important;
  display: flex !important;
  justify-content: flex-start !important;
}

.control-group .input-group {
  position: static !important;
}

.control-group .vue3-simple-typeahead {
  position: static !important;
  width: 100% !important;
}

.control-group .vue3-simple-typeahead input {
  width: 100% !important;
  padding: 8px !important;
  border: 1px solid #ddd !important;
  border-radius: 4px !important;
}

.control-group .vue3-simple-typeahead-list {
  position: absolute !important;
  top: 100% !important;
  left: 0 !important;
  width: 100% !important;
  background: white !important;
  border: 1px solid #ddd !important;
  border-radius: 4px !important;
  margin-top: 4px !important;
  z-index: 2000 !important;
  max-height: 200px !important;
  overflow-y: auto !important;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15) !important;
}

.vue3-simple-typeahead-list-item {
  padding: 4px 8px !important;
  cursor: pointer !important;
}

.vue3-simple-typeahead-list-item:hover {
  background-color: #f8f9fa !important;
}


.commandbar.collapsed {
  transform: translateX(-150%);
  visibility: hidden;
}

.toggle-btn {
  position: fixed;
  top: 80px;  /* Adjusted to match new spacing */
  left: 20px;
  background: white;
  border: 1px solid #ddd;
  padding: 8px 12px;
  border-radius: 8px;
  box-shadow: 2px 2px 6px rgba(0, 0, 0, 0.15);
  cursor: pointer;
  font-size: 20px;
  color: #666;
  z-index: 2000;
  width: 42px;
  height: 42px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.3s ease;
}

.toggle-btn:hover {
  color: #333;
  background: #f8f8f8;
}

.toggle-btn.collapsed {
  left: 20px;
}


@media (max-width: 768px) {
  .commandbar {
    top: 80px;  /* Keep consistent with map spacing */
    left: 62px;
    width: calc(100% - 82px);
    max-width: 300px;
    max-height: calc(100vh - 65px);
  }

  .toggle-btn {
    top: 80px;  /* Keep consistent with map spacing */
    left: 10px;
  }
  
  .control-group {
    margin-bottom: 10px;
  }
  
  label {
    font-size: 14px;
  }

  input[type="date"].form-control,
  input[type="number"].form-control,
  select.form-control {
    width: 100%;
  }

  .vue3-simple-typeahead,
  .vue3-simple-typeahead input,
  .vue3-simple-typeahead-list {
    width: 100%;
  }
}

.form-control {
  padding: 8px;
  border: 1px solid #ddd;
  border-radius: 4px;
}

input[type="date"].form-control,
input[type="number"].form-control {
  width: 100%;
}

select.form-control {
  width: 100%;
}


.number-input {
  display: flex;
  align-items: center;
  width: 100%;
}

.number-input input[type="number"] {
  text-align: center;
  -moz-appearance: textfield;
  width: 60px !important;
  padding: 8px 0;
  margin: 0;
  border-radius: 0;
}

.number-input input[type="number"]::-webkit-outer-spin-button,
.number-input input[type="number"]::-webkit-inner-spin-button {
  -webkit-appearance: none;
  margin: 0;
}

.number-btn {
  background-color: #f8f9fa;
  border: 1px solid #ddd;
  padding: 8px 12px;
  cursor: pointer;
  font-size: 16px;
  min-width: 36px;
}

.number-btn:first-child {
  border-radius: 4px 0 0 4px;
}

.number-btn:last-child {
  border-radius: 0 4px 4px 0;
}

.number-btn:hover:not(:disabled) {
  background-color: #e9ecef;
}

.number-btn:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

</style>
