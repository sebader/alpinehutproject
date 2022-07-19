<template>
   <section>
      <h1>Huts</h1>

      <p v-show="loading">Loading...</p>

      <div v-show="huts.length > 0">
         <table>
            <thead>
               <tr>
                  <th>ID</th>
                  <th>Hut</th>
                  <th>Country / Region</th>
                  <th>Coordinates</th>
                  <th>Link</th>
               </tr>
            </thead>
            <tbody>
               <tr v-for="hut in huts" :key="hut.id">
                  <td>{{ hut.id }}</td>
                  <td><router-link :to="{ name: 'hutDetailsPage', params: { hutId: hut.id } }" >{{ hut.name }}</router-link></td>
                  <td>{{ hut.country }} - {{ hut.region }}</td>
                  <td><router-link v-if="hut.latitude != null && hut.longitude != null" :to="{ name: 'mapPage', query: { hutId: hut.id } }" >{{ hut.latitude?.toLocaleString() }}/{{ hut.longitude?.toLocaleString() }}</router-link></td>
                  <td><a :href="`${hut.link}`" target="_blank">Online booking</a></td>
               </tr>
            </tbody>
         </table>
      </div>

      <div>
         <p v-show="!loading && huts.length > 0">Showing {{ huts.length }} hut{{ (huts.length > 1) ? "s" : "" }}.</p>
         <p v-show="!loading && huts.length <= 0">Nothing to show.</p>
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
         loading: false,
         headers: [
            { text: "ID", value: "id" },
            { text: "Hut", value: "name", sortable: true },
            { text: "Country / Region", value: "country", sortable: true },
            { text: "Link", value: "link", sortable: false }
         ]
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
   }
}
</script>