<template>
   <section>
      <p v-show="loading">{{ $t('message.loading') }}...</p>

      <div v-show="!loading">
         <label>{{ $t('message.search') }}</label>
         <input type="text" v-model="searchValue" class="form-control">
         <EasyDataTable :headers="tableHeaders" :items="extendedHuts" alternating :rows-per-page="rowsPerPage"
            :search-value="searchValue" :sort-by="sortBy" :sort-type="sortType" @update-sort="updateSort">
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
            <template v-if="isAuthenticated" #item-enabled="hut">
               <span v-if="hut.enabled" class="badge bg-success">{{ $t('message.yes') }}</span>
               <span v-else class="badge bg-danger">{{ $t('message.no') }}</span>
            </template>
            <template v-if="isAuthenticated" #item-manuallyEdited="hut">
               <span v-if="hut.manuallyEdited" class="text-primary">✓</span>
            </template>
            <template v-if="isAuthenticated" #item-actions="hut">
               <button class="btn btn-sm btn-primary me-2" @click="editHut(hut)">
                  {{ $t('message.edit') }}
               </button>
               <button class="btn btn-sm btn-danger" @click="confirmDelete(hut)">
                  {{ $t('message.delete') }}
               </button>
            </template>
         </EasyDataTable>
      </div>

      <!-- Edit Modal -->
      <div class="modal fade" id="editModal" tabindex="-1" ref="editModal">
         <div class="modal-dialog modal-lg">
            <div class="modal-content">
               <div class="modal-header">
                  <h5 class="modal-title">{{ $t('message.editHut') }}</h5>
                  <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
               </div>
               <div class="modal-body">
                  <admin-hut-form v-if="selectedHut" 
                     :hut="selectedHut" 
                     @save="saveHut" 
                     @cancel="closeEditModal" />
               </div>
            </div>
         </div>
      </div>

      <!-- Delete Confirmation Modal -->
      <div class="modal fade" id="deleteModal" tabindex="-1" ref="deleteModal">
         <div class="modal-dialog">
            <div class="modal-content">
               <div class="modal-header">
                  <h5 class="modal-title">{{ $t('message.confirmDelete') }}</h5>
                  <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
               </div>
               <div class="modal-body">
                  <p>{{ $t('message.confirmDeleteText') }} "{{ selectedHut?.name }}"?</p>
               </div>
               <div class="modal-footer">
                  <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
                     {{ $t('message.cancel') }}
                  </button>
                  <button type="button" class="btn btn-danger" @click="deleteHut">
                     {{ $t('message.delete') }}
                  </button>
               </div>
            </div>
         </div>
      </div>
   </section>
</template>

<style scoped>
section {
   padding: 0 16px;
   margin-top: -1rem;
}

.table-container {
   margin-top: 2rem;
}

.btn-sm {
   padding: 0.25rem 0.5rem;
   font-size: 0.875rem;
}

.me-2 {
   margin-right: 0.5rem;
}

.badge {
   padding: 0.5em 0.75em;
}

:deep(.vue3-easy-data-table) {
   width: 100%;
   overflow-x: auto;
   display: block;
}

:deep(.vue3-easy-data-table__main) {
   min-width: 800px;
}
</style>

<script>
import { Constants } from "../utils"
import { EventBus } from "../main"
import { Modal } from 'bootstrap';
import AdminHutForm from './AdminHutForm.vue';

export default {
   components: {
      AdminHutForm
   },
   props: {
      isAuthenticated: {
         type: Boolean,
         default: false
      }
   },
   data: function () {
      return {
         huts: [],
         rowsPerPage: 1000,
         loading: false,
         selectedHut: null,
         editModal: null,
         deleteModal: null,
         searchValue: "",
         sortBy: localStorage.getItem('sortBy') || "id",
         sortType: localStorage.getItem('sortType') || "asc"
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
      },
      // Table headers that react to authentication status
      tableHeaders() {
         const baseHeaders = [
            { text: "ID", value: "id", sortable: true },
            { text: this.$t('message.hut'), value: "name", sortable: true },
            { text: this.$t('message.country') + " / " + this.$t('message.region'), value: "countryRegion", sortable: true },
            { text: this.$t('message.coordinates'), value: "latitude", sortable: false },
            { text: "Link", value: "link", sortable: false }
         ];

         if (this.isAuthenticated) {
            baseHeaders.push(
               { text: this.$t('message.enabled'), value: "enabled", sortable: true },
               { text: this.$t('message.manuallyEdited'), value: "manuallyEdited", sortable: true },
               { text: this.$t('message.actions'), value: "actions", sortable: false }
            );
         }

         return baseHeaders;
      }
   },
   async mounted() {
      this.editModal = new Modal(this.$refs.editModal);
      this.deleteModal = new Modal(this.$refs.deleteModal);
      await this.loadHuts();
   },
   methods: {
      hutSelected(selectedHut) {
         this.$router.push({ name: 'hutDetailsPage', params: { hutId: selectedHut.id } });
      },
      updateSort(sortOptions){
         this.sortBy = sortOptions.sortBy;
         this.sortType = sortOptions.sortType;
         localStorage.setItem('sortBy', this.sortBy);
         localStorage.setItem('sortType', this.sortType);
      },
      editHut(hut) {
         this.selectedHut = { ...hut };
         this.editModal.show();
      },
      confirmDelete(hut) {
         this.selectedHut = hut;
         this.deleteModal.show();
      },
      closeEditModal() {
         this.editModal.hide();
      },
      async saveHut(updatedHut) {
         try {
            await this.$HutService.updateHutAsync(updatedHut);
            EventBus.$emit(Constants.EVENT_SUCCESS, "Hut updated successfully");
            this.closeEditModal();
            await this.loadHuts(true);
         } catch (e) {
            EventBus.$emit(Constants.EVENT_ERROR, "Error updating hut: " + e.message);
         }
      },
      async deleteHut() {
         try {
            await this.$HutService.deleteHutAsync(this.selectedHut.id);
            EventBus.$emit(Constants.EVENT_SUCCESS, "Hut deleted successfully");
            this.deleteModal.hide();
            await this.loadHuts(true);
         } catch (e) {
            EventBus.$emit(Constants.EVENT_ERROR, "Error deleting hut: " + e.message);
         }
      },
      async loadHuts(forceRefresh = false) {
         this.loading = true;
         try {
            this.huts = await this.$HutService.listHutsAsync(forceRefresh);
         } catch (e) {
            EventBus.$emit(Constants.EVENT_ERROR, "Error loading huts: " + e.message);
         }
         this.loading = false;
      }
   }
}
</script>
