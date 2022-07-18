<template>
  <div class="row justify-content-between">
    <div class="col-md-3">
      <div class="form-group">
        <form id="dateForm" class="form-inline">
          <div class="form-group">
            <div class="col-sm-4">
              <label style="width:200px">Availability at date</label>
              <input v-model="dateFilter" type="date" min="2022-07-11" style="width:200px" />
            </div>
            <div class="col-sm-2">
              <label style="width:100px">Number of beds</label>
              <input v-model="desiredNumberOfBeds" type="number" min="1" max="10" style="width:100px" />
            </div>
            <div class="col-sm-3">
              <label style="width:100px">Bed category</label>
              <select v-model="selectedBedCategory">
                <option value="">-any-</option>
                <option v-for="option in this.bedCategories" :value="option.name">
                  {{ option.name }}
                </option>
              </select>
            </div>
            <div class="col-sm-1">
              <label style="width:100px; color:white">.</label>
              <button class="btn btn-primary" onclick="resetFormInputs();">Reset</button>
            </div>
          </div>
        </form>
      </div>
    </div>
    <div class="col-md-3">
      <label>Search</label>
      <vue3-simple-typeahead id="typeahead_id" placeholder="Start writing..." :items="availabilityData"
        :minInputLength="1" :itemProjection="(item) => { return item.hutName; }" @selectItem="hutSelected">
      </vue3-simple-typeahead>
    </div>
  </div>
  <div style="height: 75vh; width: 90vw;">
    <l-map ref="map" v-model:zoom="zoom" :center="mapCenter" :minZoom="6" :maxZoom="17">
      <l-control-layers position="topright"></l-control-layers>
      <l-tile-layer v-for="tileProvider in tileProviders" :key="tileProvider.name" :name="tileProvider.name"
        :visible="tileProvider.visible" :url="tileProvider.url" :attribution="tileProvider.attribution"
        layer-type="base" />
      <template v-for="hutAvailability in this.availabilityData">
        <l-marker :ref="setMarkerItemRef" :name="hutAvailability.hutName"
          :lat-lng="[hutAvailability.latitude, hutAvailability.longitude]" :icon="getHutMarkerIcon(hutAvailability)">
          <l-tooltip>
            <b>{{ hutAvailability.hutName }}</b>
            <div v-show="hutAvailability.freeBeds != null">Free beds: {{ hutAvailability.freeBeds }}</div>
          </l-tooltip>
          <l-popup :options='{ "closeButton": false }'>
            <h6>
              <router-link :to="{ name: 'hutDetailsPage', params: { hutId: hutAvailability.hutId } }">{{
                  hutAvailability.hutName
              }}</router-link>
            </h6>
            <div>
              <span v-show="hutAvailability.freeBeds != null">[{{ new Date(this.dateFilter).toLocaleDateString() }}]
                Free beds: {{ hutAvailability.freeBeds }}</span>
              <br />
              <a v-show="hutAvailability.freeBeds != null" :href="`${hutAvailability.bookingLink}`"
                target="_blank">Online booking</a>
              <span v-show="hutAvailability.freeBeds == null">Online booking inactive</span>
              <br />
              <a :href="`${hutAvailability.hutWebsite}`" target="_blank">Hut Website</a>
              <br />
              <br />
              <template v-for="availability in hutAvailability.availabilities">
                <span>{{ availability.bedCategory }}: {{ availability.freeRoom }} / {{ availability.totalRoom }}</span>
                <br />
              </template>
              <br />
              <span>Last updated: {{ new Date(hutAvailability.lastUpdated).toLocaleString() }}</span>
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
  LPolyline,
  LPolygon,
  LRectangle,
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
    LPolyline,
    LPolygon,
    LRectangle,
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
      availabilityData: [],
      bedCategories: [],
      markerItemRefs: [],
    };
  },
  computed: {
  },
  methods: {
    async updateAvailabilityData() {
      try {
        this.availabilityData = await this.$MapviewService.getAllAvailabilityOnDate(this.dateFilter);
      }
      catch (e) {
        EventBus.$emit(Constants.EVENT_ERROR, "There was a problem fetching map data. " + e.message);
      }
    },
    setMarkerItemRef(el) {
      if (el) {
        this.markerItemRefs.push(el)
      }
    },
    // Fetch the correct marker icon based on the hut availability
    getHutMarkerIcon(hutAvailability) {
      var freeBeds = hutAvailability.freeBeds;
      if (this.selectedBedCategory != "" && hutAvailability.availabilities != null) {
        var bedCategory = this.selectedBedCategory;
        var filteredAvailability = hutAvailability.availabilities.filter(function (availability) {
          return availability.bedCategory == bedCategory;
        });
        if (filteredAvailability.length > 0) {
          freeBeds = filteredAvailability[0].freeRoom;
        }
        else {
          freeBeds = 0;
        }
      }

      var icon = ""
      // Icons from here: https://github.com/pointhi/leaflet-color-markers
      if (freeBeds == null) {
        icon = 'https://cdn.rawgit.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-grey.png'
      }
      else if (freeBeds >= this.desiredNumberOfBeds) {
        icon = 'https://cdn.rawgit.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-green.png'
      }
      else {
        icon = 'https://cdn.rawgit.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-red.png'
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
      var marker = this.markerItemRefs.find(m => m.name == hut.hutName);
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

    // If a hutId was send in the url query parameter, zoom in on the hut
    var hutIdQuery = this.$route.query.hutId;
    if (hutIdQuery != null) {
      var hutId = parseInt(hutIdQuery);
      var hut = this.availabilityData.find(hut => hut.hutId == hutId);
      if (hut != null) {
        await this.hutSelected(hut);
      }
    }
  }
};
</script>

<style>
</style>
