<template>
  <div class="commandbar row">
    <div class="col-sm-3">
      <label>{{ $t('mapPage.availabilityAtDate') }}</label>
      <div class="input-group">
        <input v-model="dateFilter" type="date" :min="`${new Date().toISOString().split('T')[0]}`" class="form-control" />
      </div>
    </div>
    <div class="col-sm-3">
      <label>{{ $t('mapPage.numberOfBeds') }}</label>
      <div class="input-group">
        <input v-model="desiredNumberOfBeds" type="number" min="1" max="10" inputmode="numeric" class="form-control" />
      </div>
    </div>
    <div class="col-sm-3">
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
    <div class="col-sm-3" style="z-index:9999;">
      <label>{{ $t('message.search') }}</label>
      <div class="input-group">
        <vue3-simple-typeahead  :items="huts" :minInputLength="1"
        :itemProjection="(hut) => { return hut.name; }" @selectItem="hutSelected">
      </vue3-simple-typeahead>
      </div>
    </div>
  </div>
    
  <div id="mainmap" style="height: 75vh; width: 100vw; ">
    <loading v-model:active="loading" />
    <l-map ref="map" v-model:zoom="zoom" :center="mapCenter" :minZoom="6" :maxZoom="17">
      <l-control-layers position="topright"></l-control-layers>
      <l-tile-layer v-for="tileProvider in tileProviders" :key="tileProvider.name" :name="tileProvider.name"
        :visible="tileProvider.visible" :url="tileProvider.url" :attribution="tileProvider.attribution"
        layer-type="base" />
      <template v-for="hut in this.huts">
        <l-marker ref="markerItems" :name="hut.name" :lat-lng="[hut.latitude, hut.longitude]"
          :icon="getHutMarkerIcon(hut)">
          <l-tooltip>
            <b>{{ hut.name }}</b>
            <div v-if="hut.availability != null && !hut.availability.hutClosed">{{ $t('mapPage.freeBeds') }}: {{
            hut.availability.totalFreeBeds }} / {{
              hut.availability.totalBeds }}</div>
            <div v-if="hut.availability?.hutClosed"><i>{{ $t('message.hutClosed') }}</i></div>  <!-- Blue marker -->
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
</template>

<script>
import { Constants } from '../utils';
import { EventBus } from "../main"
import { shortWebsiteUrl } from "../utils"

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
      mapCenter: [48.00, 11.33], // initial map center
      zoom: (() => {
        const queryZoom = parseInt(this.$route.query.zoom);
        return (!isNaN(queryZoom) && queryZoom >= 6 && queryZoom <= 17) ? queryZoom : 7;
      })(),
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
      huts: [],
      availabilityData: [],
      bedCategories: [],
      latestQueryString: this.$route.query,
    };
  },
  methods: {
    updateQueryParams(paramName, paramValue) {
      // Get all current query params and update the specified param
      const queryParams = new URLSearchParams(this.latestQueryString);
      queryParams.set(paramName, paramValue);

      // Get the hash fragment (if any)
      // remove any query params from the hash fragment
      const hashWithoutQuery = window.location.hash.split('?')[0];

      // Get current path
      const currentPath = window.location.pathname;

      // Update URL with hash fragment and query params
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
          // Map updated availability data to the huts
          this.huts.forEach(hut => {
            hut.availability = this.availabilityData.find(a => a.hutId == hut.id) || null;
          });
        }
      }
      catch (e) {
        EventBus.$emit(Constants.EVENT_ERROR, "There was a problem fetching map data. " + e.message);
      }
    },
    // Fetch the correct marker icon based on the hut availability
    getHutMarkerIcon(hut) {

      // Icons from here: https://github.com/pointhi/leaflet-color-markers
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

        if (freeBeds != null
          && this.selectedBedCategory != ""
          && hut.availability.roomAvailabilities != null) {
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
        // If there is no availability data - or the hut is closed, show the blue icon
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
    // When a hut was selected, zoom in on it and open the popup
    async hutSelected(hut) {
      this.zoom = 15;
      await this.sleep(50); // somehow the animation needs a little bit of time, otherwise it jumps to the wrong point in the map
      this.mapCenter = [hut.latitude, hut.longitude];
      var marker = this.$refs.markerItems.find(m => m.name == hut.name);
      if (marker != null) {
        marker.leafletObject.openPopup();
      }
    },
    // Little helper function
    sleep(ms) {
      return new Promise(resolve => setTimeout(resolve, ms));
    },
    async submitNotificationForm(hutId) {
      try {
        // Perform AJAX request using Fetch API
        await fetch(`/api/freebednotification/${hutId}`, {
          method: "POST",
          headers: {
            "Content-Type": "application/json"
          },
          body: JSON.stringify({
            emailAddress: this.email,
            date: this.dateFilter
          })
        });

        //console.log("Form submitted successfully!");
        this.formSubmitted = true; // Set formSubmitted to true on successful submission
      } catch (error) {
        console.error("Error submitting form:", error);
        // Handle errors
      }
    },
    onPopupClose() {
      // Reset formSubmitted to false when popup is closed
      this.formSubmitted = false;
    }
  },
  watch: {
    dateFilter: async function (newValue, oldValue) {
      this.loading = true;
      await this.updateAvailabilityData();
      this.loading = false;
      this.updateQueryParams('date', newValue);
    },
    desiredNumberOfBeds: function (newValue, oldValue) {
      this.updateQueryParams('numBeds', newValue);
    }, 
    selectedBedCategory: function (newValue, oldValue) {
      this.updateQueryParams('bedCategory', newValue);
    }
  },
  async mounted() {
    this.bedCategories = await this.$BedCategoryService.getAllBedCategories();
    await this.updateAvailabilityData();
    var allHuts = await this.$HutService.listHutsAsync();
    // Filter out huts that have not location defined as we cannot render them on the map
    var hutsWithLocation = allHuts.filter(hut => hut.latitude != null && hut.longitude != null);

    // Map availability data to the huts
    hutsWithLocation.forEach(hut => {
      hut.availability = this.availabilityData.find(a => a.hutId == hut.id) || null;
    });
    this.huts = hutsWithLocation;

    // If a hutId was send in the url query parameter, zoom in on the hut
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

</style>
