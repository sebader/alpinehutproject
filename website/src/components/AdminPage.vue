<template>
   <section>
      <p v-show="loading">{{ $t('message.loading') }}...</p>

      <div v-if="!loading">
         <h1>{{ $t('message.admin') }}</h1>

         <div class="table-responsive">
            <table class="table">
               <thead>
                  <tr>
                     <th>ID</th>
                     <th>{{ $t('message.name') }}</th>
                     <th>{{ $t('message.country') }}</th>
                     <th>{{ $t('message.region') }}</th>
                     <th>{{ $t('message.enabled') }}</th>
                     <th>{{ $t('message.actions') }}</th>
                  </tr>
               </thead>
               <tbody>
                  <tr v-for="hut in huts" :key="hut.id">
                     <td>{{ hut.id }}</td>
                     <td>{{ hut.name }}</td>
                     <td>{{ hut.country }}</td>
                     <td>{{ hut.region }}</td>
                     <td>
                        <span v-if="hut.enabled" class="badge bg-success">{{ $t('message.yes') }}</span>
                        <span v-else class="badge bg-danger">{{ $t('message.no') }}</span>
                     </td>
                     <td>
                        <button class="btn btn-sm btn-primary me-2" @click="editHut(hut)">
                           {{ $t('message.edit') }}
                        </button>
                        <button class="btn btn-sm btn-danger" @click="confirmDelete(hut)">
                           {{ $t('message.delete') }}
                        </button>
                     </td>
                  </tr>
               </tbody>
            </table>
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
         deleteModal: null
      }
   },
   methods: {
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
            await this.loadHuts();
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
.table {
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
