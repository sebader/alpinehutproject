<template>
   <section>
      <h1>Huts</h1>

      <p v-show="loading">Loading...</p>

      <div v-show="huts.length > 0">

         <div class="grid-container">
            <div v-for="hut in huts" v-bind:key="hut.id" v-on:click="gotoDetail(hut.id)" class="hut-item">
                  <strong>{{ hut.name }}</strong> <br />
                  {{ hut.country }} - {{ hut.region }}
            </div>
         </div>

         <p>Showing {{huts.length}} hut{{ (huts.length > 1) ? "s" : "" }}.</p>
      </div>

      <div v-show="!loading && huts.length <= 0">
         <p>Nothing to show.</p>
      </div>
  </section>
</template>

<style scoped>
.grid-container {
   display: grid;
   grid-template-columns: repeat(3, 1fr);
   gap: 10px;
   grid-auto-rows: minmax(100px, auto);
}
.grid-container > div {
   cursor: pointer;
}
</style>

<script>
import { Constants } from "../utils"
import { EventBus } from "../main"

export default {
   data: function() {
      return {
         huts: [],
         loading: false
      }
   },
   async created() {
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
      gotoDetail(hutId) {
         this.$router.push({ name: 'hutById', params: { hutId: hutId }});
      },
   }
}
</script>