<template>
   <section>
      <h1>{{ $t('hutListPage.allHuts') }}</h1>

      <p v-show="loading">{{ $t('message.loading') }}...</p>

      <div v-show="!loading">
         <label>{{ $t('message.search') }}</label>
         <input type="text" v-model="searchValue">
         <EasyDataTable :headers="tableHeaders" :items="extendedHuts" alternating :rows-per-page="rowsPerPage"
            :search-value="searchValue" :sort-by="sortBy">
            <template #item-name="hut">
               <router-link :to="{ name: 'hutDetailsPage', params: { hutId: hut.id } }" :title="$t('message.showHutDetails')">
                  {{ hut.name }}</router-link>
            </template>
            <template #item-latitude="hut">
               <router-link v-if="hut.latitude != null && hut.longitude != null"
                  :to="{ name: 'mapPage', query: { hutId: hut.id } }" :title="$t('message.showOnMap')">{{
                        hut.latitude?.toLocaleString()
                  }}/{{ hut.longitude?.toLocaleString() }}</router-link>
            </template>
            <template #item-link="hut">
               <a v-if="hut.enabled" :href="`${hut.link}`" target="_blank">{{ $t('message.onlineBooking') }}</a>
               <span v-else><i>{{ $t('message.onlineBookingInactive') }}</i></span>
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
            { text: this.$t('message.hut'), value: "name", sortable: true },
            { text: this.$t('message.country') + " / " + this.$t('message.region'), value: "countryRegion", sortable: true },
            { text: this.$t('message.coordinates'), value: "latitude", sortable: false },
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
            h.countryRegion = this.$t('countries.' + h.country);
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