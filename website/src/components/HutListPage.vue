<template>
   <section>
      <h1>All Huts</h1>

      <p v-show="loading">Loading...</p>

      <div v-show="!loading">
         <label>Search</label>
         <input type="text" v-model="searchValue">
         <EasyDataTable :headers="tableHeaders" :items="extendedHuts" alternating :rows-per-page="rowsPerPage"
            :search-value="searchValue" :sort-by="sortBy">
            <template #item-name="hut">
               <router-link :to="{ name: 'hutDetailsPage', params: { hutId: hut.id } }" title="Show hut details">
                  {{ hut.name }}</router-link>
            </template>
            <template #item-latitude="hut">
               <router-link v-if="hut.latitude != null && hut.longitude != null"
                  :to="{ name: 'mapPage', query: { hutId: hut.id } }" title="Show on map">{{
                        hut.latitude?.toLocaleString()
                  }}/{{ hut.longitude?.toLocaleString() }}</router-link>
            </template>
            <template #item-link="hut">
               <a v-if="hut.enabled" :href="`${hut.link}`" target="_blank">Online booking</a>
               <span v-else><i>Online booking inactive</i></span>
            </template>
         </EasyDataTable>
      </div>
   </section>
</template>

<style scoped>
</style>

<script>
import { Constants } from "../utils"
import { EventBus } from "../main"

export default {
   data: function () {
      return {
         huts: [],
         rowsPerPage: 1000,
         loading: false,
         tableHeaders: [
            { text: "ID", value: "id", sortable: true },
            { text: "Hut", value: "name", sortable: true },
            { text: "Country / Region", value: "countryRegion", sortable: true },
            { text: "Coordinates", value: "latitude", sortable: false },
            { text: "Link", value: "link", sortable: false }
         ],
         searchValue: "",
         sortBy: "id"
      }
   },
   computed: {
      // Extended huts list with additional properties
      extendedHuts() {
         return this.huts.map((h) => {
            h.countryRegion = h.country;
            if (h.region != null) {
               h.countryRegion += " - " + h.region;
            }
            return h;
         });
      }
   },
   async mounted() {
      this.loading = true;

      try {
         this.huts = await this.$HutService.listHutsAsync();
      }
      catch (e) {
         EventBus.$emit(Constants.EVENT_ERROR, "There was a problem fetching huts. " + e.message);
      }
      this.loading = false;
   },
   methods: {
      hutSelected(selectedHut) {
         this.$router.push({ name: 'hutDetailsPage', params: { hutId: selectedHut.id } });
      }
   }
}
</script>