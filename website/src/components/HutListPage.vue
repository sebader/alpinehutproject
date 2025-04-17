<template>
   <section class="container-fluid">
      <!-- Loading state -->
      <div v-show="loading" class="loading-container">
         <div class="spinner"></div>
         <p>{{ $t('message.loading') }}...</p>
      </div>

      <div v-show="!loading" class="hut-list-container">
         <!-- Hero section -->
         <div class="hero-section">
            <div class="hero-content">
               <h1 class="page-title">{{ $t('hutListPage.allHuts') }}</h1>
               <p class="page-subtitle">{{ $t('message.siteTitle') }} - {{ $t('hutListPage.hutCount', { count: huts.length }) }}</p>
            </div>
         </div>
         
         <!-- Search and filters -->
         <div class="filter-card">
            <div class="search-container">
               <label for="search-input" class="search-label">{{ $t('message.search') }}</label>
               <div class="search-input-wrapper">
                  <input type="text" id="search-input" v-model="searchValue" class="search-input" :placeholder="$t('message.searchHuts')">
                  <span class="search-icon">🔍</span>
               </div>
            </div>
         </div>
         
         <!-- Hut table -->
         <div class="data-table-container">
            <EasyDataTable 
               :headers="tableHeaders" 
               :items="extendedHuts" 
               alternating 
               :rows-per-page="rowsPerPage"
               :search-value="searchValue" 
               :sort-by="sortBy" 
               :sort-type="sortType" 
               @update-sort="updateSort"
               class="enhanced-table"
            >
               <template #item-name="hut">
                  <router-link :to="{ name: 'hutDetailsPage', params: { hutId: hut.id } }" class="hut-name-link">
                     {{ hut.name }}
                  </router-link>
               </template>
               <template #item-latitude="hut">
                  <router-link v-if="hut.latitude != null && hut.longitude != null"
                     :to="{ name: 'mapPage', query: { hutId: hut.id } }" 
                     :title="$t('message.showOnMap')"
                     class="coordinates-link">
                     <span>{{ hut.latitude?.toLocaleString() }}/{{ hut.longitude?.toLocaleString() }}</span>
                     <i class="map-icon">🗺️</i>
                  </router-link>
               </template>
               <template #item-link="hut">
                  <a v-if="hut.enabled" :href="`${hut.link}`" target="_blank" class="booking-link">
                     <span>{{ $t('message.onlineBooking') }}</span>
                     <i class="external-link-icon">↗️</i>
                  </a>
                  <span v-else class="booking-inactive">
                     <i>{{ $t('message.onlineBookingInactive') }}</i>
                  </span>
               </template>
               <template v-if="isAuthenticated" #item-enabled="hut">
                  <span v-if="hut.enabled" class="status-badge status-active">{{ $t('message.yes') }}</span>
                  <span v-else class="status-badge status-inactive">{{ $t('message.no') }}</span>
               </template>
               <template v-if="isAuthenticated" #item-manuallyEdited="hut">
                  <span v-if="hut.manuallyEdited" class="edited-mark">✓</span>
               </template>
               <template v-if="isAuthenticated" #item-actions="hut">
                  <div class="action-buttons">
                     <button class="action-btn btn-secondary btn-sm" @click="editHut(hut)">
                        {{ $t('message.edit') }}
                     </button>
                     <button class="action-btn btn-danger btn-sm" @click="confirmDelete(hut)">
                        {{ $t('message.delete') }}
                     </button>
                  </div>
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

<style scoped>
.hut-list-container {
   display: flex;
   flex-direction: column;
   gap: 1.5rem;
}

/* Hero Section */
.hero-section {
   background: linear-gradient(to right, #3494e6, #ec6ead);
   border-radius: 12px;
   padding: 2rem;
   color: white;
   box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
   position: relative;
   overflow: hidden;
}

.hero-section::before {
   content: "";
   position: absolute;
   top: 0;
   left: 0;
   right: 0;
   bottom: 0;
   background: url('https://images.unsplash.com/photo-1465056836041-7f43ac27dcb5?ixlib=rb-4.0.3&auto=format&fit=crop&w=1350&q=80') center/cover;
   opacity: 0.2;
   z-index: 0;
}

.hero-content {
   position: relative;
   z-index: 1;
}

.page-title {
   font-size: 2.2rem;
   font-weight: 700;
   margin-bottom: 0.5rem;
   text-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
}

.page-subtitle {
   font-size: 1.1rem;
   opacity: 0.9;
}

/* Search and Filters */
.search-container {
   display: flex;
   flex-direction: column;
   gap: 0.5rem;
}

.search-label {
   font-weight: 500;
   color: #555;
}

.search-input-wrapper {
   position: relative;
}

.search-input {
   width: 100%;
   padding: 12px 16px;
   border-radius: 8px;
   border: 1px solid #e0e0e0;
   font-size: 1rem;
   transition: all 0.2s;
   padding-right: 40px;
}

.search-input:focus {
   border-color: #3498db;
   box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.2);
   outline: none;
}

.search-icon {
   position: absolute;
   right: 12px;
   top: 50%;
   transform: translateY(-50%);
   color: #888;
}

/* Table Styling */
.data-table-container {
   background-color: white;
   border-radius: 12px;
   overflow: hidden;
   box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
}

:deep(.vue3-easy-data-table) {
   width: 100%;
   overflow-x: auto;
   display: block;
   border-radius: 12px;
}

:deep(.vue3-easy-data-table__main) {
   min-width: 800px;
}

:deep(.vue3-easy-data-table__header) {
   background-color: #f8f9fa !important;
}

:deep(.vue3-easy-data-table__header th) {
   font-weight: 600 !important;
   color: #555 !important;
}

:deep(.vue3-easy-data-table__body tr:nth-child(even)) {
   background-color: #f8f9fa !important;
}

:deep(.vue3-easy-data-table__body tr:hover) {
   background-color: #e9f7fe !important;
   transition: background-color 0.2s;
}

.hut-name-link {
   color: #3498db;
   text-decoration: none;
   font-weight: 500;
   transition: color 0.2s;
}

.hut-name-link:hover {
   color: #2980b9;
   text-decoration: underline;
}

.coordinates-link, .booking-link {
   color: #3498db;
   text-decoration: none;
   display: inline-flex;
   align-items: center;
   gap: 5px;
   transition: color 0.2s;
}

.coordinates-link:hover, .booking-link:hover {
   color: #2980b9;
   text-decoration: underline;
}

.map-icon, .external-link-icon {
   font-size: 0.9rem;
}

.booking-inactive {
   color: #95a5a6;
   font-style: italic;
}

.status-badge {
   display: inline-block;
   padding: 4px 10px;
   border-radius: 30px;
   font-size: 0.8rem;
   font-weight: 500;
}

.status-active {
   background-color: rgba(46, 204, 113, 0.15);
   color: #27ae60;
}

.status-inactive {
   background-color: rgba(231, 76, 60, 0.15);
   color: #c0392b;
}

.edited-mark {
   color: #3498db;
   font-weight: bold;
   font-size: 1.1rem;
}

.action-buttons {
   display: flex;
   gap: 8px;
}

.btn-sm {
   padding: 6px 12px;
   font-size: 0.85rem;
}

.btn-danger {
   background-color: #e74c3c;
}

.btn-danger:hover {
   background-color: #c0392b;
}

@media (max-width: 768px) {
   .hero-section {
      padding: 1.5rem;
   }
   
   .page-title {
      font-size: 1.8rem;
   }
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
