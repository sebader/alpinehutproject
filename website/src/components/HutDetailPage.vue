<template>
   <section>

      <p v-show="loading">Loading...</p>

      <div v-if="!loading && hut != null">
         <div class="grid-container">
            <div>
               <h1>{{ hut.name }}</h1>
               <table>
                  <tr>
                     <td>ID</td>
                     <td>{{ hut.id }}</td>
                  </tr>
                  <tr>
                     <td>Hut added</td>
                     <td>{{ new Date(hut.added).toLocaleDateString() }}</td>
                  </tr>
                  <tr>
                     <td>Last updated</td>
                     <td>{{ new Date(hut.lastUpdated).toLocaleTimeString() }}</td>
                  </tr>
                  <tr>
                     <td>Website</td>
                     <td><a :href="`${hut.hutWebsite}`" target="_blank">{{ shortWebsiteUrl }}</a></td>
                  </tr>
                  <tr v-if="hut.enabled">
                     <td></td>
                     <td><a :href="`${hut.link}`" target="_blank">Online booking</a></td>
                  </tr>
                  <tr>
                     <td>Coordinates</td>
                     <td>{{ hut.latitude?.toLocaleString() }}/{{ hut.longitude?.toLocaleString() }}</td>
                  </tr>
               </table>
            </div>
         </div>
         <br />
         <div class="grid-container">
            <table>
               <thead>
                  <tr>
                     <th>Date</th>
                     <th>Beds</th>
                     <th>Type of accommodation</th>
                  </tr>
               </thead>
               <tbody>
                  <template v-for="av in hut.availability">
                     <tr v-for="(roomAv, iSub) in av.roomAvailabilities" :key="roomAv.bedCategory">
                        <td v-if="iSub === 0" :rowspan="av.roomAvailabilities.length">{{ new
                              Date(av.date).toDateString("dddd, dd.MM.yyyy")
                        }}</td>
                        <td>{{ roomAv.freeBeds }} / {{ roomAv.totalBeds }}</td>
                        <td>{{ roomAv.bedCategory }}</td>
                     </tr>
                  </template>
               </tbody>
            </table>
         </div>
      </div>
   </section>
</template>

<style scoped>
.grid-container {
   display: grid;
   grid-template-columns: 1fr 1fr;
   gap: 10px;
   grid-auto-rows: minmax(100px, auto);
}

.availability-box {
   border: 2px dashed rgb(233, 233, 233);
   padding: 8px;
}

.availability-box .name {
   font-size: 9pt
}
</style>

<script>
import { Constants } from '../utils';
import { EventBus } from "../main"

export default {
   data: function () {
      return {

         loading: false
      }
   },
   computed: {
      shortWebsiteUrl () {
         const regex = new RegExp("^http[s]{0,1}://(www\.){0,1}(.*?)(/.*){0,1}$");
         const matches = regex.exec(this.hut.hutWebsite);
         return matches && matches[2];
      }
   },
   methods: {
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
         }
      }
      catch (e) {
         EventBus.$emit(Constants.EVENT_ERROR, "There was a problem fetching the hut. " + e.message);
      }

      this.loading = false;
   }
}
</script>