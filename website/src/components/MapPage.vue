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
      <div class="autocomplete">
        <label>Search</label>
        <input autocomplete="off" id="mapSearch" class="form-control" type="text" placeholder="Search">
      </div>
    </div>
  </div>
  <div style="height: 75vh; width: 90vw;">
    <l-map v-model="zoom" v-model:zoom="zoom" :center="[46.90, 11.33]" :minZoom="6" :maxZoom="17">
      <l-control-layers position="topright"></l-control-layers>
      <l-tile-layer v-for="tileProvider in tileProviders" :key="tileProvider.name" :name="tileProvider.name"
        :visible="tileProvider.visible" :url="tileProvider.url" :attribution="tileProvider.attribution"
        layer-type="base" />
      <template v-for="hutAvailability in this.availabilityData">
        <l-marker :lat-lng="[hutAvailability.latitude, hutAvailability.longitude]"
          :icon="getIcon(hutAvailability)">
          <l-tooltip>
            <b>{{ hutAvailability.hutName }}</b>
            <div v-show="hutAvailability.freeBeds != null">Free beds: {{ hutAvailability.freeBeds }}</div>
          </l-tooltip>
          <l-popup :options='{"closeButton": false}'>
            <h6>
              <router-link :to="{ name: 'hutById', params: { hutId: hutAvailability.hutId } }">{{
                  hutAvailability.hutName
              }}</router-link>
            </h6>
            <div>
              <span v-show="hutAvailability.freeBeds != null">[{{ new Date(this.dateFilter).toLocaleDateString() }}] Free beds: {{ hutAvailability.freeBeds }}</span>
              <br />
              <a v-show="hutAvailability.freeBeds != null" :href="`${hutAvailability.bookingLink}`" target="_blank">Online booking</a>
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
      zoom: 7,
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
    };
  },
  computed: {
  },
  methods: {
    async updateMap() {
      try {
        this.availabilityData = await this.$MapviewService.getAllAvailabilityOnDate(this.dateFilter);
      }
      catch (e) {
        EventBus.$emit(Constants.EVENT_ERROR, "There was a problem fetching map data. " + e.message);
      }
    },
    getIcon(hutAvailability) {

      var freeBeds = hutAvailability.freeBeds;
      if(this.selectedBedCategory != "" && hutAvailability.availabilities != null) {
        var bedCategory = this.selectedBedCategory;
        var filteredAvailability = hutAvailability.availabilities.filter(function(availability) {
          return availability.bedCategory == bedCategory;
        });
        if(filteredAvailability.length > 0) {
          freeBeds = filteredAvailability[0].freeRoom;
        }
        else{
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
  },
  watch: {
    dateFilter: function (newValue, oldValue) {
      this.updateMap();
    }
  },
  async mounted() {
    this.bedCategories = await this.$BedCategoryService.getAllBedCategories();
    await this.updateMap();
  }
};



function mapRequestHandler() {
  // if AJAX returned a list of markers, add them to the map
  if (ajaxRequest.readyState == 4) {
    //use the info here that was returned
    if (ajaxRequest.status == 200) {
      hutlist = eval("(" + ajaxRequest.responseText + ")");
      plotHutMarkers()

      // If URL parameter hutid was set, we jump to that hut and open its popup
      var params = getUrlVars();
      if (params["hutid"] !== undefined) {
        centerOnHut(params["hutid"]);
      }
    }
  }
}


function centerOnHut(hutId) {
  var hut = hutlist.find(hut => hut.id == hutId);
  if (hut !== undefined && hut !== null) {
    map.setView(new L.LatLng(hut.latitude, hut.longitude), 15);
    plotlayers.find(l => l.data.id == hut.id).openPopup();
  }
}


// Search with autocomplete
function autocomplete(inp) {
  /*the autocomplete function takes two arguments,
  the text field element and an array of possible autocompleted values:*/
  var currentFocus;
  /*execute a function when someone writes in the text field:*/
  inp.addEventListener("input", function (e) {
    var a, b, i, val = this.value;
    /*close any already open lists of autocompleted values*/
    closeAllLists();
    if (!val) { return false; }
    currentFocus = -1;
    /*create a DIV element that will contain the items (values):*/
    a = document.createElement("DIV");
    a.setAttribute("id", this.id + "autocomplete-list");
    a.setAttribute("class", "autocomplete-items");
    /*append the DIV element as a child of the autocomplete container:*/
    this.parentNode.appendChild(a);
    /*for each item in the array...*/
    var arr = hutlist;
    for (i = 0; i < arr.length; i++) {
      /*check if the item starts with the same letters as the text field value:*/
      var valField = arr[i].name; // We are searching on the name
      if (valField.substr(0, val.length).toUpperCase() == val.toUpperCase()) {
        /*create a DIV element for each matching element:*/
        b = document.createElement("DIV");
        /*make the matching letters bold:*/
        b.innerHTML = "<strong>" + valField.substr(0, val.length) + "</strong>";
        b.innerHTML += valField.substr(val.length);
        /*insert a input field that will hold the current array item's value:*/
        b.innerHTML += "<input type='hidden' value='" + valField + "'>";
        /*execute a function when someone clicks on the item value (DIV element):*/
        b.addEventListener("click", function (e) {
          /*insert the value for the autocomplete text field:*/
          inp.value = this.getElementsByTagName("input")[0].value;

          var hut = hutlist.find(poi => poi.name === inp.value);
          map.setView(new L.LatLng(hut.latitude, hut.longitude), 15);
          // Open the popup
          plotlayers.find(l => l.data.id == hut.id).openPopup();

          /*close the list of autocompleted values,
          (or any other open lists of autocompleted values:*/
          closeAllLists();
          // Do the search
        });
        a.appendChild(b);
      }
    }
  });
  /*execute a function presses a key on the keyboard:*/
  inp.addEventListener("keydown", function (e) {
    var x = document.getElementById(this.id + "autocomplete-list");
    if (x) x = x.getElementsByTagName("div");
    if (e.keyCode == 40) {
      /*If the arrow DOWN key is pressed,
      increase the currentFocus variable:*/
      currentFocus++;
      /*and and make the current item more visible:*/
      addActive(x);
    } else if (e.keyCode == 38) { //up
      /*If the arrow UP key is pressed,
      decrease the currentFocus variable:*/
      currentFocus--;
      /*and and make the current item more visible:*/
      addActive(x);
    } else if (e.keyCode == 13) {
      /*If the ENTER key is pressed, prevent the form from being submitted,*/
      e.preventDefault();
      if (currentFocus > -1) {
        /*and simulate a click on the "active" item:*/
        if (x) x[currentFocus].click();
      }
    }
  });
  function addActive(x) {
    /*a function to classify an item as "active":*/
    if (!x) return false;
    /*start by removing the "active" class on all items:*/
    removeActive(x);
    if (currentFocus >= x.length) currentFocus = 0;
    if (currentFocus < 0) currentFocus = (x.length - 1);
    /*add class "autocomplete-active":*/
    x[currentFocus].classList.add("autocomplete-active");
  }
  function removeActive(x) {
    /*a function to remove the "active" class from all autocomplete items:*/
    for (var i = 0; i < x.length; i++) {
      x[i].classList.remove("autocomplete-active");
    }
  }
  function closeAllLists(elmnt) {
    /*close all autocomplete lists in the document,
    except the one passed as an argument:*/
    var x = document.getElementsByClassName("autocomplete-items");
    for (var i = 0; i < x.length; i++) {
      if (elmnt != x[i] && elmnt != inp) {
        x[i].parentNode.removeChild(x[i]);
      }
    }
  }
  /*execute a function when someone clicks in the document:*/
  document.addEventListener("click", function (e) {
    closeAllLists(e.target);
  });
}
</script>

<style>
</style>
