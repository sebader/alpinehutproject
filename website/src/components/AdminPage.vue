<template>
   <section>
      <p v-show="loading">{{ $t('message.loading') }}...</p>

      <div v-if="!loading">
         <h1>{{ $t('message.admin') }}</h1>

         <div class="table-container">
            <EasyDataTable 
               :headers="tableHeaders" 
               :items="huts" 
               alternating 
               :rows-per-page="rowsPerPage"
               :sort-by="sortBy"
               :sort-type="sortType"
               @update-sort="updateSort"
            >
               <template #item-name="item">
                  <router-link :to="{ name: 'hutDetailsPage', params: { hutId: item.id } }" :title="$t('message.showHutDetails')">
                     {{ item.name }}
                  </router-link>
               </template>
               <template #item-enabled="item">
                  <span v-if="item.enabled" class="badge bg-success">{{ $t('message.yes') }}</span>
                  <span v-else class="badge bg-danger">{{ $t('message.no') }}</span>
               </template>
               <template #item-manuallyEdited="item">
                  <span v-if="item.manuallyEdited" class="text-primary">✓</span>
               </template>
               <template #item-actions="item">
                  <button class="btn btn-sm btn-primary me-2" @click="editHut(item)">
                     {{ $t('message.edit') }}
                  </button>
                  <button class="btn btn-sm btn-danger" @click="confirmDelete(item)">
                     {{ $t('message.delete') }}
                  </button>
               </template>
            </EasyDataTable>
         </div>
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

<script>
import { Modal } from 'bootstrap';
import { EventBus } from "../main";
import { Constants } from "../utils";
import AdminHutForm from './AdminHutForm.vue';

export default {
   components: {
      AdminHutForm
   },
   data() {
      return {
         huts: [],
         loading: false,
         selectedHut: null,
         editModal: null,
         deleteModal: null,
         rowsPerPage: 1000,
         tableHeaders: [
            { text: "ID", value: "id", sortable: true },
            { text: this.$t('message.name'), value: "name", sortable: true },
            { text: this.$t('message.country'), value: "country", sortable: true },
            { text: this.$t('message.region'), value: "region", sortable: true },
            { text: this.$t('message.enabled'), value: "enabled", sortable: false },
            { text: this.$t('message.manuallyEdited'), value: "manuallyEdited", sortable: false },
            { text: this.$t('message.actions'), value: "actions", sortable: false }
         ],
         sortBy: localStorage.getItem('adminSortBy') || "id",
         sortType: localStorage.getItem('adminSortType') || "asc"
      }
   },
   methods: {
      updateSort(sortOptions) {
         this.sortBy = sortOptions.sortBy;
         this.sortType = sortOptions.sortType;
         localStorage.setItem('adminSortBy', this.sortBy);
         localStorage.setItem('adminSortType', this.sortType);
      },
      async loadHuts(forceRefresh = false) {
         this.loading = true;
         try {
            this.huts = await this.$HutService.listHutsAsync(forceRefresh);
         } catch (e) {
            EventBus.$emit(Constants.EVENT_ERROR, "Error loading huts: " + e.message);
         }
         this.loading = false;
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
      }
   },
   mounted() {
      this.editModal = new Modal(this.$refs.editModal);
      this.deleteModal = new Modal(this.$refs.deleteModal);
      this.loadHuts();
   }
}
</script>

<style scoped>
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
</style>
