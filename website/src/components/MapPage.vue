<template>
  <div class="row">
    <div class="col-sm-3">
      <label>Availability at date</label>
      <input v-model="dateFilter" type="date" min="2022-07-11" style="width:200px" />
    </div>
    <div class="col-sm-3">
      <label>Number of beds</label>
      <input v-model="desiredNumberOfBeds" type="number" min="1" max="10" style="width:100px" />
    </div>
    <div class="col-sm-3">
      <label>Bed category</label>
      <select v-model="selectedBedCategory">
        <option value="">-any-</option>
        <option v-for="option in this.bedCategories" :value="option.name">
          {{ option.name }}
        </option>
      </select>
    </div>
    <div class="col-sm-3">
      <label>Search</label>
      <vue3-simple-typeahead style="z-index:10000;" placeholder="Start writing..." :items="huts" :minInputLength="1"
        :itemProjection="(hut) => { return hut.name; }" @selectItem="hutSelected">
      </vue3-simple-typeahead>
    </div>
  </div>
  <div style="height: 75vh; width: 90vw;">
    <l-map ref="map" v-model:zoom="zoom" :center="mapCenter" :minZoom="6" :maxZoom="17">
      <l-control-layers position="topright"></l-control-layers>
      <l-tile-layer v-for="tileProvider in tileProviders" :key="tileProvider.name" :name="tileProvider.name"
        :visible="tileProvider.visible" :url="tileProvider.url" :attribution="tileProvider.attribution"
        layer-type="base" />
      <template v-for="hut in this.huts">
        <l-marker ref="markerItems" :name="hut.name" :lat-lng="[hut.latitude, hut.longitude]"
          :icon="getHutMarkerIcon(hut.availability)">
          <l-tooltip>
            <b>{{ hut.name }}</b>
            <div v-if="hut.availability != null">Free beds: {{ hut.availability.totalFreeBeds }}</div>
          </l-tooltip>
          <l-popup :options='{ "closeButton": false }'>
            <h6>
              <router-link :to="{ name: 'hutDetailsPage', params: { hutId: hut.id } }">{{
                  hut.name
              }}</router-link>
            </h6>
            <div>
              <span v-if="hut.availability != null">[{{ new Date(this.dateFilter).toLocaleDateString()
              }}]
                Free beds: {{ hut.availability.totalFreeBeds }}</span>
              <br />
              <a v-if="hut.availability != null" :href="`${hut.link}`" target="_blank">Online booking</a>
              <span v-else>Online booking inactive</span>
              <br />
              <a :href="`${hut.website}`" target="_blank">Hut Website</a>
              <br />
              <br />
              <template v-for="availability in hut.availability?.roomAvailabilities">
                <span>{{ availability.bedCategory }}: {{ availability.freeBeds }} / {{ availability.totalBeds }}</span>
                <br />
              </template>
              <br />
              <span>Last updated: {{ new
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

export default {
  components: {
    LMap,
    LIcon,
    LTileLayer,
    LMarker,
    LControlLayers,
    LTooltip,
    LPopup,
  },
  data() {
    return {
      dateFilter: new Date().toISOString().split('T')[0],
      selectedBedCategory: "",
      desiredNumberOfBeds: 1,
      mapCenter: [46.90, 11.33], // initial map center
      zoom: 7,  // initial zoom level
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
    };
  },
  computed: {
  },
  methods: {
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
    getHutMarkerIcon(hutAvailability) {

      var icon = "";
      if (hutAvailability == null) {
        icon = 'https://cdn.rawgit.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-grey.png'
      }
      else {
        var freeBeds = hutAvailability.totalFreeBeds;
        if (this.selectedBedCategory != "" && hutAvailability.roomAvailabilities != null) {
          var bedCategory = this.selectedBedCategory;
          var filteredAvailability = hutAvailability.roomAvailabilities.filter(function (availability) {
            return availability.bedCategory == bedCategory;
          });
          if (filteredAvailability.length > 0) {
            freeBeds = filteredAvailability[0].freeBeds;
          }
          else {
            freeBeds = 0;
          }
        }
        // Icons from here: https://github.com/pointhi/leaflet-color-markers
        if (freeBeds == null) {
          icon = 'https://cdn.rawgit.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-blue.png'
        }
        else if (freeBeds >= this.desiredNumberOfBeds) {
          icon = 'https://cdn.rawgit.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-green.png'
        }
        else {
          icon = 'https://cdn.rawgit.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-red.png'
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
    }
  },
  watch: {
    dateFilter: function (newValue, oldValue) {
      this.updateAvailabilityData();
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
  }
};
</script>

<style>
</style>
